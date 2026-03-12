using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using neTiPx.Models;

namespace neTiPx.Services
{
    public sealed class NetworkConfigService
    {
        public (bool success, string? error) ApplyProfile(IpProfile profile)
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
                var (valid, errorMessage) = ValidateIpGatewaySubnet(first.IpAddress, first.SubnetMask, profile.Gateway);
                if (!valid)
                {
                    return (false, errorMessage);
                }

                var addressCmd = $"netsh interface ipv4 set address name=\"{ni.Name}\" source=static addr={first.IpAddress} mask={first.SubnetMask}";
                if (IsValidIPv4(profile.Gateway))
                {
                    addressCmd += $" gateway={profile.Gateway} gwmetric=1";
                }
                commands.Add(addressCmd);

                for (int i = 1; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var (entryValid, entryError) = ValidateIpGatewaySubnet(entry.IpAddress, entry.SubnetMask, string.Empty);
                    if (!entryValid)
                    {
                        return (false, $"Validierungsfehler in IP #{i + 1}: {entryError}");
                    }

                    commands.Add($"netsh interface ipv4 add address name=\"{ni.Name}\" addr={entry.IpAddress} mask={entry.SubnetMask}");
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

            var prefix = $"{destination}/{prefixLength}";

            if (!RouteExists(ni.Name, prefix))
            {
                return (true, "Route nicht vorhanden.");
            }

            var commands = new List<string>
            {
                $"netsh interface ipv4 delete route prefix={prefix} interface=\"{ni.Name}\""
            };

            return RunNetshCommandsElevated(commands);
        }

        public (bool success, List<RouteEntry> routes, string? error) ReadStaticRoutes(IpProfile profile)
        {
            if (string.IsNullOrWhiteSpace(profile.AdapterName))
            {
                return (false, new List<RouteEntry>(), "Kein Adapter ausgewaehlt.");
            }

            var ni = FindNetworkInterface(profile.AdapterName);
            if (ni == null)
            {
                return (false, new List<RouteEntry>(), "Netzwerkadapter nicht gefunden.");
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netsh interface ipv4 show route interface=\"{ni.Name}\"",
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
                    return (false, new List<RouteEntry>(), "Routen konnten nicht eingelesen werden.");
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var routes = ParseStaticRoutes(output);

                if (routes.Count == 0)
                {
                    var persistentOutput = ReadRoutePrintOutput();
                    routes = ParsePersistentRoutes(persistentOutput);
                }

                return (true, routes, null);
            }
            catch (Exception ex)
            {
                return (false, new List<RouteEntry>(), "Fehler beim Einlesen der Routen: " + ex.Message);
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
                    return (false, "netsh wurde nicht erfolgreich ausgefuehrt.");
                }

                return (true, null);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return (false, "Berechtigung erforderlich: Bitte UAC bestaetigen.");
            }
            catch (Exception ex)
            {
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
            if (!IsValidIPv4(subnet))
            {
                return false;
            }

            var bytes = System.Net.IPAddress.Parse(subnet).GetAddressBytes();
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

            return true;
        }

        private static (bool valid, string errorMessage) ValidateIpGatewaySubnet(string ip, string subnet, string gateway)
        {
            if (string.IsNullOrWhiteSpace(ip))
            {
                return (false, "IP-Adresse ist erforderlich.");
            }
            if (!IsValidIPv4(ip))
            {
                return (false, $"IP-Adresse '{ip}' ist ungueltig.");
            }

            if (string.IsNullOrWhiteSpace(subnet))
            {
                return (false, "Subnetzmaske ist erforderlich.");
            }
            if (!IsValidSubnetMask(subnet))
            {
                return (false, $"Subnetzmaske '{subnet}' ist ungueltig.");
            }

            if (!string.IsNullOrWhiteSpace(gateway))
            {
                if (!IsValidIPv4(gateway))
                {
                    return (false, $"Gateway '{gateway}' ist ungueltig.");
                }

                if (!IsIpInSubnet(ip, gateway, subnet))
                {
                    return (false, $"Gateway '{gateway}' passt nicht zum Subnetz der IP '{ip}' mit Maske '{subnet}'.");
                }
            }

            return (true, string.Empty);
        }

        private static bool IsIpInSubnet(string ip, string testIp, string subnet)
        {
            try
            {
                if (!System.Net.IPAddress.TryParse(ip, out var ipAddr) ||
                    !System.Net.IPAddress.TryParse(testIp, out var testAddr) ||
                    !System.Net.IPAddress.TryParse(subnet, out var subnetAddr))
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
            if (!IsValidSubnetMask(subnet))
            {
                return -1;
            }

            var bytes = System.Net.IPAddress.Parse(subnet).GetAddressBytes();
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
                if (!System.Net.IPAddress.TryParse(destination, out var destinationAddr) ||
                    !System.Net.IPAddress.TryParse(subnet, out var subnetAddr))
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

        private static bool RouteExists(string interfaceName, string prefix)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c netsh interface ipv4 show route interface=\"{interfaceName}\"",
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
                    return false;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static List<RouteEntry> ParseStaticRoutes(string output)
        {
            var routes = new List<RouteEntry>();
            if (string.IsNullOrWhiteSpace(output))
            {
                return routes;
            }

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.IndexOf("0.0.0.0/0", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                var prefixMatch = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"(?<prefix>(?:\d{1,3}\.){3}\d{1,3}/\d{1,2})");

                if (!prefixMatch.Success)
                {
                    continue;
                }

                var prefix = prefixMatch.Groups["prefix"].Value.Trim();

                var ipv4Matches = System.Text.RegularExpressions.Regex.Matches(
                    line,
                    @"\b(?:\d{1,3}\.){3}\d{1,3}\b");

                if (ipv4Matches.Count == 0)
                {
                    continue;
                }

                // In netsh output the last IPv4 token is typically the nexthop/gateway.
                var gateway = ipv4Matches[ipv4Matches.Count - 1].Value.Trim();

                if (!TryParsePrefix(prefix, out var destination, out var subnetMask))
                {
                    continue;
                }

                if (!IsValidIPv4(gateway))
                {
                    continue;
                }

                var metric = 1;
                var metricMatch = System.Text.RegularExpressions.Regex.Match(line, @"\b(?<metric>\d{1,5})\b");
                if (metricMatch.Success && int.TryParse(metricMatch.Groups["metric"].Value, out var parsedMetric) && parsedMetric > 0)
                {
                    metric = parsedMetric;
                }

                routes.Add(new RouteEntry
                {
                    Destination = destination,
                    SubnetMask = subnetMask,
                    Gateway = gateway,
                    Metric = metric
                });
            }

            return routes
                .GroupBy(route => $"{route.Destination}|{route.SubnetMask}|{route.Gateway}|{route.Metric}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
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

        private static List<RouteEntry> ParsePersistentRoutes(string output)
        {
            var routes = new List<RouteEntry>();
            if (string.IsNullOrWhiteSpace(output))
            {
                return routes;
            }

            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            bool inPersistentSection = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.IndexOf("Persistente Routen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("Persistent Routes", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    inPersistentSection = true;
                    continue;
                }

                if (!inPersistentSection)
                {
                    continue;
                }

                // End section when another heading starts.
                if (line.EndsWith(":", StringComparison.Ordinal) &&
                    line.IndexOf("Persistente Routen", StringComparison.OrdinalIgnoreCase) < 0 &&
                    line.IndexOf("Persistent Routes", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    break;
                }

                var match = System.Text.RegularExpressions.Regex.Match(
                    line,
                    @"^(?<destination>(?:\d{1,3}\.){3}\d{1,3})\s+(?<mask>(?:\d{1,3}\.){3}\d{1,3})\s+(?<gateway>(?:\d{1,3}\.){3}\d{1,3})\s+(?<metric>\d+)$");

                if (!match.Success)
                {
                    continue;
                }

                var destination = match.Groups["destination"].Value;
                var mask = match.Groups["mask"].Value;
                var gateway = match.Groups["gateway"].Value;
                var metric = int.TryParse(match.Groups["metric"].Value, out var parsedMetric) && parsedMetric > 0 ? parsedMetric : 1;

                if (!IsValidIPv4(destination) || !IsValidSubnetMask(mask) || !IsValidIPv4(gateway))
                {
                    continue;
                }

                routes.Add(new RouteEntry
                {
                    Destination = destination,
                    SubnetMask = mask,
                    Gateway = gateway,
                    Metric = metric
                });
            }

            return routes
                .GroupBy(route => $"{route.Destination}|{route.SubnetMask}|{route.Gateway}|{route.Metric}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
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
