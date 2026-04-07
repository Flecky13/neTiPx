using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using neTiPx.Helpers;
using neTiPx.Models;

namespace neTiPx.Services
{
    public sealed class NetworkConfigService
    {
        private sealed class RouteReadSnapshot
        {
            public string RoutePrintOutput { get; init; } = string.Empty;
            public (bool success, List<RouteEntry> routes, string? error) CimResult { get; init; }
                = (false, new List<RouteEntry>(), null);
            public (bool success, List<RouteEntry> routes, string? error) NetRouteResult { get; init; }
                = (false, new List<RouteEntry>(), null);
        }

        public (bool success, string? error) ApplyProfile(IpProfile profile)
        {
            DebugLogger.Log(LogLevel.INFO, "NetConfig", $"ApplyProfile start: Profil='{profile.Name}', Adapter='{profile.AdapterName}', Modus='{profile.Mode}'");

            if (string.IsNullOrWhiteSpace(profile.AdapterName))
            {
                DebugLogger.Log(LogLevel.WARN, "NetConfig", "ApplyProfile abgebrochen: kein Adapter gewählt");
                return (false, "Kein Adapter ausgewaehlt.");
            }

            var ni = FindNetworkInterface(profile.AdapterName);
            if (ni == null)
            {
                return (false, "Netzwerkadapter nicht gefunden.");
            }

            var commands = new List<string>();
            if (profile.Mode.Equals("DHCP", StringComparison.OrdinalIgnoreCase))
            {
                commands.Add($"netsh interface ipv4 set address name=\"{ni.Name}\" source=dhcp");
                commands.Add($"netsh interface ipv4 set dns name=\"{ni.Name}\" source=dhcp");
            }
            else
            {
                var entries = profile.IpAddresses.Where(e => !string.IsNullOrWhiteSpace(e.IpAddress)).ToList();
                if (entries.Count == 0)
                {
                    return (false, "Mindestens eine IP-Adresse ist erforderlich.");
                }

                var first = entries[0];
                var (valid, errorMessage, normalizedFirstSubnetMask) = ValidateIpGatewaySubnet(first.IpAddress, first.SubnetMask, profile.Gateway);
                if (!valid)
                {
                    return (false, errorMessage);
                }

                var addressCmd = $"netsh interface ipv4 set address name=\"{ni.Name}\" source=static addr={first.IpAddress} mask={normalizedFirstSubnetMask}";
                if (IsValidIPv4(profile.Gateway))
                {
                    addressCmd += $" gateway={profile.Gateway} gwmetric=1";
                }
                commands.Add(addressCmd);

                for (int i = 1; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var (entryValid, entryError, normalizedSubnetMask) = ValidateIpGatewaySubnet(entry.IpAddress, entry.SubnetMask, string.Empty);
                    if (!entryValid)
                    {
                        return (false, $"Validierungsfehler in IP #{i + 1}: {entryError}");
                    }

                    commands.Add($"netsh interface ipv4 add address name=\"{ni.Name}\" addr={entry.IpAddress} mask={normalizedSubnetMask}");
                }

                // Set DNS servers
                if (IsValidIPv4(profile.Dns1))
                {
                    commands.Add($"netsh interface ipv4 set dns name=\"{ni.Name}\" source=static addr={profile.Dns1} register=primary");

                    if (IsValidIPv4(profile.Dns2))
                    {
                        commands.Add($"netsh interface ipv4 add dns name=\"{ni.Name}\" addr={profile.Dns2} index=2");
                    }
                }
                else if (IsValidIPv4(profile.Dns2))
                {
                    // If only DNS2 is set, use it as primary
                    commands.Add($"netsh interface ipv4 set dns name=\"{ni.Name}\" source=static addr={profile.Dns2} register=primary");
                }
            }

            if (profile.RoutesEnabled)
            {
                if (!profile.AddRoutesOnApply)
                {
                    var routeSnapshot = ReadRouteSnapshot(includeRoutePrint: true, includeCim: true, includeNetRoutes: false);
                    var persistentRoutes = GetPersistentRoutesFromSnapshot(routeSnapshot);

                    foreach (var persistentRoute in persistentRoutes)
                    {
                        if (!IsValidIPv4(persistentRoute.Destination) || SubnetMaskToPrefix(persistentRoute.SubnetMask) <= 0)
                        {
                            continue;
                        }

                        commands.Add($"route delete {persistentRoute.Destination} mask {persistentRoute.SubnetMask} {persistentRoute.Gateway}");
                    }
                }

                for (int i = 0; i < profile.Routes.Count; i++)
                {
                    var route = profile.Routes[i];
                    var (routeValid, routeError) = ValidateRoute(route);
                    if (!routeValid)
                    {
                        return (false, $"Routen-Validierung #{i + 1}: {routeError}");
                    }

                    var prefixLength = SubnetMaskToPrefix(route.SubnetMask);
                    if (prefixLength <= 0)
                    {
                        return (false, $"Routen-Validierung #{i + 1}: Subnetzmaske ist ungueltig.");
                    }

                    var prefix = $"{route.Destination}/{prefixLength}";
                    var metric = route.Metric > 0 ? route.Metric : 1;

                    // Replace existing route if present.
                    commands.Add($"netsh interface ipv4 delete route prefix={prefix} interface=\"{ni.Name}\" >nul 2>&1");
                    commands.Add($"netsh interface ipv4 add route prefix={prefix} interface=\"{ni.Name}\" nexthop={route.Gateway} metric={metric}");
                }
            }

            return RunNetshCommandsElevated(commands);
        }

        public (bool success, List<RouteEntry> routes, string? error) ReadAllPersistentRoutes()
        {
            try
            {
                var routeSnapshot = ReadRouteSnapshot(includeRoutePrint: true, includeCim: true, includeNetRoutes: true);
                var allRoutes = ParseRoutePrintRoutes(routeSnapshot.RoutePrintOutput, includeActiveRoutes: true, includePersistentRoutes: true);

                if (allRoutes.Count == 0)
                {
                    if (routeSnapshot.CimResult.success)
                    {
                        allRoutes = routeSnapshot.CimResult.routes;
                    }
                }

                var persistentRouteKeys = new HashSet<string>(
                    routeSnapshot.CimResult.routes.Select(route => BuildRouteKey(route.Destination, route.SubnetMask, route.Gateway)),
                    StringComparer.OrdinalIgnoreCase);

                var userRouteKeys = new HashSet<string>(
                    routeSnapshot.NetRouteResult.routes
                        .Where(route => route.CanDeleteFromSystem)
                        .Select(route => BuildRouteKey(route.Destination, route.SubnetMask, route.Gateway)),
                    StringComparer.OrdinalIgnoreCase);

                var normalized = allRoutes
                    .GroupBy(r => $"{r.Destination}|{r.SubnetMask}|{r.Gateway}|{r.Metric}", StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var first = g.First();
                        var key = BuildRouteKey(first.Destination, first.SubnetMask, first.Gateway);
                        first.CanDeleteFromSystem = persistentRouteKeys.Contains(key) || userRouteKeys.Contains(key);
                        return first;
                    })
                    .ToList();

                return (true, normalized, null);
            }
            catch (Exception ex)
            {
                return (false, new List<RouteEntry>(), "Fehler beim Einlesen der Routen: " + ex.Message);
            }
        }

        public (bool success, string? error) DeleteRoute(RouteEntry route)
        {
            var destination = route.Destination?.Trim() ?? string.Empty;
            var subnetMask = route.SubnetMask?.Trim() ?? string.Empty;

            DebugLogger.Log(LogLevel.INFO, "NetConfig", $"DeleteRoute: {destination} mask {subnetMask} via {route.Gateway}");

            if (!IsValidIPv4(destination))
            {
                DebugLogger.Log(LogLevel.WARN, "NetConfig", $"DeleteRoute abgebrochen: Zieladresse '{destination}' ist ungültig");
                return (false, "Zieladresse ist ungültig.");
            }

            if (SubnetMaskToPrefix(subnetMask) <= 0)
            {
                DebugLogger.Log(LogLevel.WARN, "NetConfig", $"DeleteRoute abgebrochen: Subnetzmaske '{subnetMask}' ist ungültig");
                return (false, "Subnetzmaske ist ungültig.");
            }

            if (!TryNormalizeSubnetMask(subnetMask, out var normalizedSubnetMask))
            {
                return (false, "Subnetzmaske ist ungültig.");
            }

            var commands = new List<string>
            {
                $"route delete {destination} mask {normalizedSubnetMask} {route.Gateway}"
            };

            return RunNetshCommandsElevated(commands);
        }

        public (bool success, string? error) AddRouteStandalone(RouteEntry route)
        {
            var sanitizedRoute = new RouteEntry
            {
                Destination = route.Destination?.Trim() ?? string.Empty,
                SubnetMask = route.SubnetMask?.Trim() ?? string.Empty,
                Gateway = route.Gateway?.Trim() ?? string.Empty,
                Metric = route.Metric > 0 ? route.Metric : 1
            };

            DebugLogger.Log(LogLevel.INFO, "NetConfig", $"AddRouteStandalone: {sanitizedRoute.Destination} mask {sanitizedRoute.SubnetMask} via {sanitizedRoute.Gateway} metric {sanitizedRoute.Metric}");

            var (isValid, validationError) = ValidateRoute(sanitizedRoute);
            if (!isValid)
            {
                DebugLogger.Log(LogLevel.WARN, "NetConfig", $"AddRouteStandalone Validierung fehlgeschlagen: {validationError}");
                return (false, validationError);
            }

            if (!TryNormalizeSubnetMask(sanitizedRoute.SubnetMask, out var normalizedSubnetMask))
            {
                return (false, $"Subnetzmaske '{sanitizedRoute.SubnetMask}' ist ungueltig.");
            }

            var commands = new List<string>
            {
                $"route -p add {sanitizedRoute.Destination} mask {normalizedSubnetMask} {sanitizedRoute.Gateway} metric {sanitizedRoute.Metric}"
            };

            return RunNetshCommandsElevated(commands);
        }

        public (bool success, string? error) RemoveRoute(IpProfile profile, RouteEntry route)
        {
            if (string.IsNullOrWhiteSpace(profile.AdapterName))
            {
                return (false, "Kein Adapter ausgewaehlt.");
            }

            var ni = FindNetworkInterface(profile.AdapterName);
            if (ni == null)
            {
                return (false, "Netzwerkadapter nicht gefunden.");
            }

            var destination = route.Destination?.Trim() ?? string.Empty;
            var subnetMask = route.SubnetMask?.Trim() ?? string.Empty;

            if (!IsValidIPv4(destination))
            {
                return (false, "Route kann nicht geloescht werden: Zieladresse ist ungueltig.");
            }

            var prefixLength = SubnetMaskToPrefix(subnetMask);
            if (prefixLength <= 0)
            {
                return (false, "Route kann nicht geloescht werden: Subnetzmaske ist ungueltig.");
            }

            if (!TryNormalizeSubnetMask(subnetMask, out var normalizedSubnetMask))
            {
                return (false, "Route kann nicht geloescht werden: Subnetzmaske ist ungueltig.");
            }

            var prefix = $"{destination}/{prefixLength}";

            var persistentRoutes = GetPersistentRoutesForLookup();
            if (!PersistentRouteExists(destination, normalizedSubnetMask, route.Gateway, persistentRoutes))
            {
                return (true, "Route nicht vorhanden.");
            }

            var commands = new List<string>
            {
                $"route delete {destination} mask {normalizedSubnetMask} {route.Gateway}"
            };

            return RunNetshCommandsElevated(commands);
        }

        public (bool success, string? error) AddRoute(IpProfile profile, RouteEntry route)
        {
            if (string.IsNullOrWhiteSpace(profile.AdapterName))
            {
                return (false, "Kein Adapter ausgewaehlt.");
            }

            var ni = FindNetworkInterface(profile.AdapterName);
            if (ni == null)
            {
                return (false, "Netzwerkadapter nicht gefunden.");
            }

            var sanitizedRoute = new RouteEntry
            {
                Destination = route.Destination?.Trim() ?? string.Empty,
                SubnetMask = route.SubnetMask?.Trim() ?? string.Empty,
                Gateway = route.Gateway?.Trim() ?? string.Empty,
                Metric = route.Metric > 0 ? route.Metric : 1
            };

            var (isValid, validationError) = ValidateRoute(sanitizedRoute);
            if (!isValid)
            {
                return (false, validationError);
            }

            if (!TryNormalizeSubnetMask(sanitizedRoute.SubnetMask, out var normalizedSubnetMask))
            {
                return (false, $"Subnetzmaske '{sanitizedRoute.SubnetMask}' ist ungueltig.");
            }

            var persistentRoutes = GetPersistentRoutesForLookup();
            if (PersistentRouteExists(sanitizedRoute.Destination, normalizedSubnetMask, sanitizedRoute.Gateway, persistentRoutes))
            {
                return (true, "Route bereits vorhanden.");
            }

            var commands = new List<string>
            {
                $"route -p add {sanitizedRoute.Destination} mask {normalizedSubnetMask} {sanitizedRoute.Gateway} metric {sanitizedRoute.Metric}"
            };

            return RunNetshCommandsElevated(commands);
        }

        public (bool success, List<RouteEntry> routes, string? error, string debugInfo) ReadStaticRoutes(IpProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.AdapterName))
            {
                return (false, new List<RouteEntry>(), "Kein Adapter ausgewaehlt.", "AdapterName im Profil ist leer.");
            }

            var ni = FindNetworkInterface(profile.AdapterName);
            if (ni == null)
            {
                return (false, new List<RouteEntry>(), "Netzwerkadapter nicht gefunden.", $"Adapter '{profile.AdapterName}' konnte nicht aufgeloest werden.");
            }

            try
            {
                var debug = new StringBuilder();
                debug.AppendLine($"AdapterKey: {profile.AdapterName}");
                debug.AppendLine($"Resolved Adapter: {ni.Name}");

                var routeSnapshot = ReadRouteSnapshot(includeRoutePrint: true, includeCim: true, includeNetRoutes: false);
                if (routeSnapshot.CimResult.success)
                {
                    debug.AppendLine($"CIM source: Win32_IP4PersistedRouteTable");
                    debug.AppendLine($"parsed persistent routes via CIM: {routeSnapshot.CimResult.routes.Count}");
                    return (true, routeSnapshot.CimResult.routes, null, debug.ToString());
                }

                debug.AppendLine($"CIM read failed: {routeSnapshot.CimResult.error ?? "unknown error"}");

                debug.AppendLine($"route print output chars: {routeSnapshot.RoutePrintOutput.Length}");

                var routes = ParseRoutePrintRoutes(routeSnapshot.RoutePrintOutput, includeActiveRoutes: false, includePersistentRoutes: true);
                debug.AppendLine($"parsed persistent routes via route print fallback: {routes.Count}");
                debug.AppendLine("route print preview:");
                debug.AppendLine(CreatePreview(routeSnapshot.RoutePrintOutput));

                return (true, routes, null, debug.ToString());
            }
            catch (Exception ex)
            {
                return (false, new List<RouteEntry>(), "Fehler beim Einlesen der Routen: " + ex.Message, ex.ToString());
            }
        }

        private static NetworkInterface? FindNetworkInterface(string adapterKey)
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => !n.IsReceiveOnly)
                .Where(n => n.Speed > 0)
                .FirstOrDefault(n => string.Equals(n.Name, adapterKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Description, adapterKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Name + " - " + n.Description, adapterKey, StringComparison.OrdinalIgnoreCase));
        }

        private static (bool success, string? error) RunNetshCommandsElevated(IReadOnlyList<string> commands)
        {
            if (commands.Count == 0)
            {
                return (true, null);
            }

            DebugLogger.Log(LogLevel.INFO, "NetConfig", $"Netsh-Befehle starten ({commands.Count} Befehl(e))");
            foreach (var cmd in commands)
                DebugLogger.Log(LogLevel.INFO, "NetConfig", $"  CMD: {cmd}");

            try
            {
                var joined = string.Join(" & ", commands);
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c " + joined,
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                process?.WaitForExit();
                if (process == null || process.ExitCode != 0)
                {
                    DebugLogger.Log(LogLevel.ERROR, "NetConfig", $"Netsh fehlgeschlagen (ExitCode={process?.ExitCode})");
                    return (false, "netsh wurde nicht erfolgreich ausgefuehrt.");
                }

                DebugLogger.Log(LogLevel.INFO, "NetConfig", "Netsh-Befehle erfolgreich ausgeführt");
                return (true, null);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                DebugLogger.Log(LogLevel.WARN, "NetConfig", "UAC abgebrochen oder Berechtigung verweigert", ex);
                return (false, "Berechtigung erforderlich: Bitte UAC bestaetigen.");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogLevel.ERROR, "NetConfig", "RunNetshCommandsElevated fehlgeschlagen", ex);
                return (false, "Fehler beim Anwenden: " + ex.Message);
            }
        }

        private static bool IsValidIPv4(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return false;
            }

            if (System.Net.IPAddress.TryParse(ip, out var addr))
            {
                return addr.AddressFamily == AddressFamily.InterNetwork;
            }

            return false;
        }

        private static bool IsValidSubnetMask(string subnet)
        {
            return TryNormalizeSubnetMask(subnet, out _);
        }

        private static (bool valid, string errorMessage, string normalizedSubnetMask) ValidateIpGatewaySubnet(string ip, string subnet, string gateway)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return (false, "IP-Adresse ist erforderlich.", string.Empty);
            }
            if (!IsValidIPv4(ip))
            {
                return (false, $"IP-Adresse '{ip}' ist ungueltig.", string.Empty);
            }

            if (string.IsNullOrWhiteSpace(subnet))
            {
                return (false, "Subnetzmaske ist erforderlich.", string.Empty);
            }
            if (!TryNormalizeSubnetMask(subnet, out var normalizedSubnet))
            {
                return (false, $"Subnetzmaske '{subnet}' ist ungueltig.", string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(gateway))
            {
                if (!IsValidIPv4(gateway))
                {
                    return (false, $"Gateway '{gateway}' ist ungueltig.", string.Empty);
                }

                if (!IsIpInSubnet(ip, gateway, normalizedSubnet))
                {
                    return (false, $"Gateway '{gateway}' passt nicht zum Subnetz der IP '{ip}' mit Maske '{subnet}'.", string.Empty);
                }
            }

            return (true, string.Empty, normalizedSubnet);
        }

        private static bool IsIpInSubnet(string ip, string testIp, string subnet)
        {
            try
            {
                if (!TryNormalizeSubnetMask(subnet, out var normalizedSubnet))
                {
                    return false;
                }

                if (!System.Net.IPAddress.TryParse(ip, out var ipAddr) ||
                    !System.Net.IPAddress.TryParse(testIp, out var testAddr) ||
                    !System.Net.IPAddress.TryParse(normalizedSubnet, out var subnetAddr))
                {
                    return false;
                }

                var ipBytes = ipAddr.GetAddressBytes();
                var testBytes = testAddr.GetAddressBytes();
                var subnetBytes = subnetAddr.GetAddressBytes();

                uint ipUint = ((uint)ipBytes[0] << 24) | ((uint)ipBytes[1] << 16) | ((uint)ipBytes[2] << 8) | ipBytes[3];
                uint testUint = ((uint)testBytes[0] << 24) | ((uint)testBytes[1] << 16) | ((uint)testBytes[2] << 8) | testBytes[3];
                uint subnetUint = ((uint)subnetBytes[0] << 24) | ((uint)subnetBytes[1] << 16) | ((uint)subnetBytes[2] << 8) | subnetBytes[3];

                uint ipNetwork = ipUint & subnetUint;
                uint testNetwork = testUint & subnetUint;

                return ipNetwork == testNetwork;
            }
            catch
            {
                return false;
            }
        }

        private static (bool valid, string errorMessage) ValidateRoute(RouteEntry route)
        {
            if (string.IsNullOrWhiteSpace(route.Destination) || !IsValidIPv4(route.Destination))
            {
                return (false, $"Ziel '{route.Destination}' ist ungueltig.");
            }

            if (string.IsNullOrWhiteSpace(route.SubnetMask) || !IsValidSubnetMask(route.SubnetMask))
            {
                return (false, $"Subnetzmaske '{route.SubnetMask}' ist ungueltig.");
            }

            if (string.IsNullOrWhiteSpace(route.Gateway) || !IsValidIPv4(route.Gateway))
            {
                return (false, $"Gateway '{route.Gateway}' ist ungueltig.");
            }

            if (!IsNetworkAddress(route.Destination, route.SubnetMask))
            {
                return (false, $"Ziel '{route.Destination}' ist keine gueltige Netzadresse fuer '{route.SubnetMask}'.");
            }

            if (route.Metric <= 0)
            {
                return (false, "Metrik muss groesser als 0 sein.");
            }

            return (true, string.Empty);
        }

        private static int SubnetMaskToPrefix(string subnet)
        {
            if (!TryNormalizeSubnetMask(subnet, out var normalizedSubnet))
            {
                return -1;
            }

            var bytes = System.Net.IPAddress.Parse(normalizedSubnet).GetAddressBytes();
            uint mask = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];

            int prefix = 0;
            for (int i = 31; i >= 0; i--)
            {
                if ((mask & (1u << i)) != 0)
                {
                    prefix++;
                }
                else
                {
                    break;
                }
            }

            return prefix;
        }

        private static bool IsNetworkAddress(string destination, string subnet)
        {
            try
            {
                if (!TryNormalizeSubnetMask(subnet, out var normalizedSubnet))
                {
                    return false;
                }

                if (!System.Net.IPAddress.TryParse(destination, out var destinationAddr) ||
                    !System.Net.IPAddress.TryParse(normalizedSubnet, out var subnetAddr))
                {
                    return false;
                }

                var destinationBytes = destinationAddr.GetAddressBytes();
                var subnetBytes = subnetAddr.GetAddressBytes();

                uint destinationUint = ((uint)destinationBytes[0] << 24) | ((uint)destinationBytes[1] << 16) | ((uint)destinationBytes[2] << 8) | destinationBytes[3];
                uint subnetUint = ((uint)subnetBytes[0] << 24) | ((uint)subnetBytes[1] << 16) | ((uint)subnetBytes[2] << 8) | subnetBytes[3];

                return (destinationUint & subnetUint) == destinationUint;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryNormalizeSubnetMask(string input, out string normalizedSubnetMask)
        {
            normalizedSubnetMask = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var value = input.Trim();
            if (value.StartsWith("/", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            if (int.TryParse(value, out var prefixLength))
            {
                if (prefixLength <= 0 || prefixLength > 32)
                {
                    return false;
                }

                normalizedSubnetMask = PrefixLengthToSubnetMask(prefixLength);
                return true;
            }

            if (!IsValidIPv4(value))
            {
                return false;
            }

            var bytes = System.Net.IPAddress.Parse(value).GetAddressBytes();
            uint mask = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
            if (mask == 0)
            {
                return false;
            }

            bool seenZero = false;
            for (int i = 31; i >= 0; i--)
            {
                bool bitSet = (mask & (1u << i)) != 0;
                if (!bitSet)
                {
                    seenZero = true;
                }
                else if (seenZero)
                {
                    return false;
                }
            }

            normalizedSubnetMask = value;
            return true;
        }

        private static bool PersistentRouteExists(string destination, string subnetMask, string gateway, List<RouteEntry>? persistentRoutes = null)
        {
            var routes = persistentRoutes ?? GetPersistentRoutesForLookup();

            return routes.Any(route =>
                string.Equals(route.Destination, destination, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(route.SubnetMask, subnetMask, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(route.Gateway, gateway, StringComparison.OrdinalIgnoreCase));
        }

        private static List<RouteEntry> GetPersistentRoutesForLookup()
        {
            var routeSnapshot = ReadRouteSnapshot(includeRoutePrint: true, includeCim: true, includeNetRoutes: false);
            return GetPersistentRoutesFromSnapshot(routeSnapshot);
        }

        private static List<RouteEntry> GetPersistentRoutesFromSnapshot(RouteReadSnapshot routeSnapshot)
        {
            if (routeSnapshot.CimResult.success)
            {
                return routeSnapshot.CimResult.routes;
            }

            return ParseRoutePrintRoutes(routeSnapshot.RoutePrintOutput, includeActiveRoutes: false, includePersistentRoutes: true);
        }

        private static RouteReadSnapshot ReadRouteSnapshot(bool includeRoutePrint, bool includeCim, bool includeNetRoutes)
        {
            return new RouteReadSnapshot
            {
                RoutePrintOutput = includeRoutePrint ? ReadRoutePrintOutput() : string.Empty,
                CimResult = includeCim
                    ? TryReadPersistentRoutesFromCim()
                    : (false, new List<RouteEntry>(), null),
                NetRouteResult = includeNetRoutes
                    ? TryReadNetRoutesFromPowerShell()
                    : (false, new List<RouteEntry>(), null)
            };
        }

        private static (bool success, List<RouteEntry> routes, string? error) TryReadPersistentRoutesFromCim()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"@(Get-CimInstance -ClassName Win32_IP4PersistedRouteTable | Select-Object Destination,Mask,NextHop,Metric1) | ConvertTo-Json -Compress\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, new List<RouteEntry>(), "powershell process could not be started");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return (false, new List<RouteEntry>(), string.IsNullOrWhiteSpace(error) ? $"powershell exit code {process.ExitCode}" : error.Trim());
                }

                var trimmed = output.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    return (true, new List<RouteEntry>(), null);
                }

                using var document = JsonDocument.Parse(trimmed);
                var parsed = new List<RouteEntry>();
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in document.RootElement.EnumerateArray())
                    {
                        AddCimRoute(parsed, element);
                    }
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    AddCimRoute(parsed, document.RootElement);
                }

                return (true,
                    parsed
                        .GroupBy(route => $"{route.Destination}|{route.SubnetMask}|{route.Gateway}|{route.Metric}", StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList(),
                    null);
            }
            catch (Exception ex)
            {
                return (false, new List<RouteEntry>(), ex.Message);
            }
        }

        private static void AddCimRoute(List<RouteEntry> routes, JsonElement element)
        {
            var destination = GetJsonStringValue(element, "Destination");
            var subnetMask = GetJsonStringValue(element, "Mask");
            var gateway = GetJsonStringValue(element, "NextHop");
            var metricText = GetJsonStringValue(element, "Metric1");

            if (!int.TryParse(metricText, out var metric) || metric <= 0)
            {
                metric = 1;
            }

            if (!IsValidIPv4(destination) || !IsValidSubnetMask(subnetMask) || !IsValidIPv4(gateway))
            {
                return;
            }

            routes.Add(new RouteEntry
            {
                Destination = destination,
                SubnetMask = subnetMask,
                Gateway = gateway,
                Metric = metric,
                CanDeleteFromSystem = true
            });
        }

        private static string GetJsonStringValue(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
            {
                return string.Empty;
            }

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString()?.Trim() ?? string.Empty,
                JsonValueKind.Number => property.GetRawText().Trim(),
                JsonValueKind.Null => string.Empty,
                _ => property.GetRawText().Trim('"', ' ')
            };
        }

        private static string ReadRoutePrintOutput()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c route print -4",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return string.Empty;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return output;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static (bool success, List<RouteEntry> routes, string? error) TryReadNetRoutesFromPowerShell()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"@(Get-NetRoute -AddressFamily IPv4 | Select-Object DestinationPrefix,NextHop,RouteMetric,Protocol) | ConvertTo-Json -Compress\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, new List<RouteEntry>(), "powershell process could not be started");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return (false, new List<RouteEntry>(), string.IsNullOrWhiteSpace(error) ? $"powershell exit code {process.ExitCode}" : error.Trim());
                }

                var trimmed = output.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    return (true, new List<RouteEntry>(), null);
                }

                using var document = JsonDocument.Parse(trimmed);
                var parsed = new List<RouteEntry>();
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in document.RootElement.EnumerateArray())
                    {
                        AddNetRoute(parsed, element);
                    }
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    AddNetRoute(parsed, document.RootElement);
                }

                return (true,
                    parsed
                        .GroupBy(route => $"{route.Destination}|{route.SubnetMask}|{route.Gateway}|{route.Metric}", StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList(),
                    null);
            }
            catch (Exception ex)
            {
                return (false, new List<RouteEntry>(), ex.Message);
            }
        }

        private static void AddNetRoute(List<RouteEntry> routes, JsonElement element)
        {
            var destinationPrefix = GetJsonStringValue(element, "DestinationPrefix");
            var nextHop = GetJsonStringValue(element, "NextHop");
            var routeMetricText = GetJsonStringValue(element, "RouteMetric");
            var protocol = GetJsonStringValue(element, "Protocol");

            if (string.IsNullOrWhiteSpace(destinationPrefix) || !TryParsePrefix(destinationPrefix, out var destination, out var subnetMask))
            {
                return;
            }

            if (!int.TryParse(routeMetricText, out var metric) || metric <= 0)
            {
                metric = 1;
            }

            var normalizedGateway = string.IsNullOrWhiteSpace(nextHop) ? "On-link" : nextHop;
            var canDelete = string.Equals(protocol, "NetMgmt", StringComparison.OrdinalIgnoreCase)
                || string.Equals(protocol, "Static", StringComparison.OrdinalIgnoreCase);

            routes.Add(new RouteEntry
            {
                Destination = destination,
                SubnetMask = subnetMask,
                Gateway = normalizedGateway,
                Metric = metric,
                CanDeleteFromSystem = canDelete
            });
        }

        private static string BuildRouteKey(string destination, string subnetMask, string gateway)
        {
            return $"{destination?.Trim()}|{subnetMask?.Trim()}|{gateway?.Trim()}";
        }

        private static List<RouteEntry> ParseRoutePrintRoutes(string output, bool includeActiveRoutes, bool includePersistentRoutes)
        {
            var routes = new List<RouteEntry>();
            if (string.IsNullOrWhiteSpace(output))
            {
                return routes;
            }

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var section = RoutePrintSection.None;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (IsActiveRoutesHeading(line))
                {
                    section = RoutePrintSection.ActiveRoutes;
                    continue;
                }

                if (IsPersistentRoutesHeading(line))
                {
                    section = RoutePrintSection.PersistentRoutes;
                    continue;
                }

                if (section == RoutePrintSection.None)
                {
                    continue;
                }

                if (IsSectionDivider(line) || IsColumnHeading(line))
                {
                    continue;
                }

                if (line.IndexOf("IPv6", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    break;
                }

                var parts = System.Text.RegularExpressions.Regex
                    .Split(line, @"\s{2,}")
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .ToArray();

                if (section == RoutePrintSection.ActiveRoutes)
                {
                    if (!includeActiveRoutes || parts.Length < 5)
                    {
                        continue;
                    }

                    AddParsedRoute(routes, parts[0], parts[1], parts[2], parts[4], canDeleteFromSystem: false);
                    continue;
                }

                if (!includePersistentRoutes || parts.Length < 4)
                {
                    continue;
                }

                AddParsedRoute(routes, parts[0], parts[1], parts[2], parts[3], canDeleteFromSystem: true);
            }

            return routes
                .GroupBy(route => $"{route.Destination}|{route.SubnetMask}|{route.Gateway}|{route.Metric}", StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var first = group.First();
                    first.CanDeleteFromSystem = group.Any(r => r.CanDeleteFromSystem);
                    return first;
                })
                .ToList();
        }

        private static void AddParsedRoute(List<RouteEntry> routes, string destination, string mask, string gateway, string metricText, bool canDeleteFromSystem)
        {
            var normalizedDestination = destination.Trim();
            var normalizedMask = mask.Trim();
            var normalizedGateway = gateway.Trim();
            var isDefaultRoute = string.Equals(normalizedDestination, "0.0.0.0", StringComparison.OrdinalIgnoreCase)
                && string.Equals(normalizedMask, "0.0.0.0", StringComparison.OrdinalIgnoreCase);

            if (!int.TryParse(metricText.Trim(), out var metric) || metric <= 0)
            {
                metric = 1;
            }

            if (!IsValidIPv4(normalizedDestination) || (!IsValidSubnetMask(normalizedMask) && !isDefaultRoute))
            {
                return;
            }

            routes.Add(new RouteEntry
            {
                Destination = normalizedDestination,
                SubnetMask = normalizedMask,
                Gateway = normalizedGateway,
                Metric = metric,
                CanDeleteFromSystem = canDeleteFromSystem
            });
        }

        private static bool IsActiveRoutesHeading(string line)
        {
            return line.Equals("Aktive Routen:", StringComparison.OrdinalIgnoreCase) ||
                   line.Equals("Active Routes:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPersistentRoutesHeading(string line)
        {
             return line.Equals("Ständige Routen:", StringComparison.OrdinalIgnoreCase) ||
                 line.Equals("Staendige Routen:", StringComparison.OrdinalIgnoreCase) ||
                 line.Equals("Persistente Routen:", StringComparison.OrdinalIgnoreCase) ||
                   line.Equals("Persistent Routes:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsColumnHeading(string line)
        {
            return line.StartsWith("Netzwerkziel", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Network Destination", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSectionDivider(string line)
        {
            return line.All(ch => ch == '=');
        }

        private enum RoutePrintSection
        {
            None,
            ActiveRoutes,
            PersistentRoutes
        }

        private static string CreatePreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "<leer>";
            }

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Where(line => line != null)
                .Take(20)
                .Select(line => line.TrimEnd())
                .ToList();

            return string.Join(Environment.NewLine, lines);
        }

        private static bool TryParsePrefix(string prefix, out string destination, out string subnetMask)
        {
            destination = string.Empty;
            subnetMask = string.Empty;

            var parts = prefix.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            destination = parts[0].Trim();
            if (!IsValidIPv4(destination))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0 || prefixLength > 32)
            {
                return false;
            }

            subnetMask = PrefixLengthToSubnetMask(prefixLength);
            return !string.IsNullOrWhiteSpace(subnetMask);
        }

        private static string PrefixLengthToSubnetMask(int prefixLength)
        {
            if (prefixLength < 0 || prefixLength > 32)
            {
                return string.Empty;
            }

            uint mask = prefixLength == 0 ? 0 : uint.MaxValue << (32 - prefixLength);
            var bytes = new[]
            {
                (byte)((mask >> 24) & 0xFF),
                (byte)((mask >> 16) & 0xFF),
                (byte)((mask >> 8) & 0xFF),
                (byte)(mask & 0xFF)
            };

            return new System.Net.IPAddress(bytes).ToString();
        }
    }
}
