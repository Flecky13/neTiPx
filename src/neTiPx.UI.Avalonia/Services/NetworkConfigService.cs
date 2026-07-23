using neTiPx.UI.Avalonia.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using neTiPx.UI.Avalonia.Services;
using neTiPx.Core.Models;

namespace neTiPx.UI.Avalonia.Services
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
            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"ApplyProfile start: Profil='{profile.Name}', Adapter='{profile.AdapterName}', Modus='{profile.Mode}'");

            if (string.IsNullOrWhiteSpace(profile.AdapterName))
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", "ApplyProfile abgebrochen: kein Adapter gewählt");
                return (false, "Kein Adapter ausgewaehlt.");
            }

            // OS-spezifische Implementierung
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return ApplyProfileLinux(profile);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ApplyProfileWindows(profile);
            }
            else
            {
                return (false, "Betriebssystem wird nicht unterstützt.");
            }
        }

        private (bool success, string? error) ApplyProfileWindows(IpProfile profile)
        {
            var ni = FindNetworkInterface(profile.AdapterName!);
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
                var routeStore = IsPersistentRouteMode(profile.RoutePersistenceMode) ? "persistent" : "active";
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
                    commands.Add($"netsh interface ipv4 add route prefix={prefix} interface=\"{ni.Name}\" nexthop={route.Gateway} metric={metric} store={routeStore}");
                }
            }

            return RunNetshCommandsElevated(commands);
        }

        private (bool success, string? error) ApplyProfileLinux(IpProfile profile)
        {
            var ni = FindNetworkInterface(profile.AdapterName!);
            if (ni == null)
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"Netzwerkadapter '{profile.AdapterName}' nicht gefunden");
                return (false, $"Netzwerkadapter '{profile.AdapterName}' nicht gefunden. Bitte Adapter neu laden.");
            }

            // Finde die NetworkManager-Connection für dieses Device
            var connectionName = FindNmcliConnection(ni.Name);
            if (connectionName == null)
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"Keine NetworkManager-Verbindung für Device '{ni.Name}' erstellt werden konnte");
                return (false, $"Keine NetworkManager-Verbindung für Device '{ni.Name}' konnte erstellt werden. Bitte prüfen Sie die NetworkManager-Konfiguration.");
            }

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Verwende NetworkManager Connection: '{connectionName}' für Device: '{ni.Name}'");

            var commands = new List<string>();
            var runtimeRouteCommands = new List<string>();
            var usePersistentRoutes = IsPersistentRouteMode(profile.RoutePersistenceMode);

            if (profile.Mode.Equals("DHCP", StringComparison.OrdinalIgnoreCase))
            {
                // DHCP-Modus: Setze automatische IP-Konfiguration in einem Befehl
                commands.Add($"nmcli con mod \"{connectionName}\" ipv4.method auto ipv4.addresses \"\" ipv4.gateway \"\" ipv4.dns \"\"");
                
                // Routen verwalten auch im DHCP-Modus
                if (profile.RoutesEnabled && profile.Routes.Count > 0)
                {
                    var modeText = usePersistentRoutes ? "Persistent" : "Temporary";
                    LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Setze {profile.Routes.Count} Routen (DHCP-Modus, {modeText})");
                    
                    // Lösche alle bestehenden Routen
                    commands.Add($"nmcli con mod \"{connectionName}\" ipv4.routes \"\"");
                    
                    foreach (var routeSpec in BuildLinuxRouteSpecs(profile.Routes))
                    {
                        if (usePersistentRoutes)
                        {
                            commands.Add($"nmcli con mod \"{connectionName}\" +ipv4.routes \"{EscapeShellDoubleQuoted(routeSpec)}\"");
                        }
                        else
                        {
                            runtimeRouteCommands.Add(BuildLinuxRuntimeRouteCommand(routeSpec));
                        }
                    }
                }
                else
                {
                    // Lösche alle Routen, wenn keine aktiv sind
                    commands.Add($"nmcli con mod \"{connectionName}\" ipv4.routes \"\"");
                }
                
                commands.Add($"nmcli con up \"{connectionName}\"");
            }
            else
            {
                // Static-Modus: Validiere und setze statische IP-Konfiguration
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

                // Konvertiere Subnetzmaske zu Prefix-Länge
                var prefixLength = SubnetMaskToPrefix(normalizedFirstSubnetMask);
                if (prefixLength <= 0)
                {
                    return (false, "Subnetzmaske ist ungueltig.");
                }

                // Baue IP-Adress-Liste im Format "ip/prefix ip2/prefix2 ..."
                var ipAddresses = new List<string>();
                ipAddresses.Add($"{first.IpAddress}/{prefixLength}");

                for (int i = 1; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    var (entryValid, entryError, normalizedSubnetMask) = ValidateIpGatewaySubnet(entry.IpAddress, entry.SubnetMask, string.Empty);
                    if (!entryValid)
                    {
                        return (false, $"Validierungsfehler in IP #{i + 1}: {entryError}");
                    }

                    var entryPrefix = SubnetMaskToPrefix(normalizedSubnetMask);
                    if (entryPrefix <= 0)
                    {
                        return (false, $"Subnetzmaske in IP #{i + 1} ist ungueltig.");
                    }

                    ipAddresses.Add($"{entry.IpAddress}/{entryPrefix}");
                }

                // Baue IP-Adress-Liste im Format "ip/prefix,ip2/prefix2,..." (Komma-getrennt für nmcli!)
                var ipAddressList = string.Join(",", ipAddresses);

                // Baue DNS-Server-Liste
                var dnsServers = new List<string>();
                if (IsValidIPv4(profile.Dns1))
                {
                    dnsServers.Add(profile.Dns1);
                }
                if (IsValidIPv4(profile.Dns2))
                {
                    dnsServers.Add(profile.Dns2);
                }
                // DNS-Server mit Komma trennen für nmcli
                var dnsList = dnsServers.Count > 0 ? string.Join(",", dnsServers) : "";

                // Baue einen einzigen nmcli con mod Befehl mit allen IPv4-Einstellungen
                // Das verhindert Validierungsfehler, wenn method auf manual gesetzt wird
                var modCommand = $"nmcli con mod \"{connectionName}\" ipv4.method manual ipv4.addresses \"{ipAddressList}\"";
                
                if (IsValidIPv4(profile.Gateway))
                {
                    modCommand += $" ipv4.gateway \"{profile.Gateway}\"";
                }
                else
                {
                    modCommand += " ipv4.gateway \"\"";
                }

                if (!string.IsNullOrEmpty(dnsList))
                {
                    modCommand += $" ipv4.dns \"{dnsList}\"";
                }
                else
                {
                    modCommand += " ipv4.dns \"\"";
                }

                commands.Add(modCommand);

                // Routen verwalten (vor dem Aktivieren der Verbindung)
                if (profile.RoutesEnabled && profile.Routes.Count > 0)
                {
                    var modeText = usePersistentRoutes ? "Persistent" : "Temporary";
                    LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Setze {profile.Routes.Count} Routen (Static-Modus, {modeText})");
                    
                    // Lösche alle bestehenden Routen
                    commands.Add($"nmcli con mod \"{connectionName}\" ipv4.routes \"\"");
                    
                    foreach (var routeSpec in BuildLinuxRouteSpecs(profile.Routes))
                    {
                        if (usePersistentRoutes)
                        {
                            commands.Add($"nmcli con mod \"{connectionName}\" +ipv4.routes \"{EscapeShellDoubleQuoted(routeSpec)}\"");
                        }
                        else
                        {
                            runtimeRouteCommands.Add(BuildLinuxRuntimeRouteCommand(routeSpec));
                        }
                    }
                }
                else
                {
                    // Lösche alle Routen, wenn keine aktiv sind
                    commands.Add($"nmcli con mod \"{connectionName}\" ipv4.routes \"\"");
                }

                // Aktiviere die Verbindung
                commands.Add($"nmcli con up \"{connectionName}\"");
            }

            var nmcliResult = RunNmcliCommandsElevated(commands);
            if (!nmcliResult.success)
            {
                return nmcliResult;
            }

            if (runtimeRouteCommands.Count == 0)
            {
                return (true, null);
            }

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Wende {runtimeRouteCommands.Count} temporäre Route(n) per ip route an");
            return RunShellCommandsElevated(runtimeRouteCommands);
        }

        private static bool IsPersistentRouteMode(string? routePersistenceMode)
        {
            return !string.Equals(routePersistenceMode, "Temporary", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> BuildLinuxRouteSpecs(IEnumerable<RouteEntry> routes)
        {
            var result = new List<string>();
            foreach (var route in routes)
            {
                if (string.IsNullOrWhiteSpace(route.Destination) ||
                    string.IsNullOrWhiteSpace(route.SubnetMask) ||
                    string.IsNullOrWhiteSpace(route.Gateway))
                {
                    continue;
                }

                var routePrefixLength = SubnetMaskToPrefix(route.SubnetMask);
                if (routePrefixLength <= 0)
                {
                    continue;
                }

                var metric = route.Metric > 0 ? route.Metric : 100;
                result.Add($"{route.Destination}/{routePrefixLength} {route.Gateway} {metric}");
            }

            return result;
        }

        private static string BuildLinuxRuntimeRouteCommand(string nmcliRouteSpec)
        {
            var parts = nmcliRouteSpec
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return $"ip route replace {nmcliRouteSpec}";
            }

            var destinationPrefix = parts[0];
            var gateway = parts[1];
            var metric = parts.Length >= 3 && int.TryParse(parts[2], out var parsedMetric) && parsedMetric > 0
                ? parsedMetric
                : 100;

            return $"ip route replace {destinationPrefix} via {gateway} metric {metric}";
        }

        private static string EscapeShellDoubleQuoted(string input)
        {
            return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string? FindNmcliConnection(string deviceName)
        {
            try
            {
                // Prüfe zuerst, ob eine Verbindung für dieses Device existiert
                var psi = new ProcessStartInfo
                {
                    FileName = "nmcli",
                    Arguments = "-t -f NAME,DEVICE connection show",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return null;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return null;
                }

                // Parse output: "ConnectionName:DeviceName"
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var connName = parts[0].Trim();
                        var connDevice = parts[1].Trim();

                        if (string.Equals(connDevice, deviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Gefundene Verbindung: '{connName}' für Device: '{deviceName}'");
                            return connName;
                        }
                    }
                }

                // Keine Verbindung gefunden - erstelle eine neue
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Keine Verbindung für Device '{deviceName}' gefunden, erstelle neue Verbindung");
                return CreateNmcliConnection(deviceName);
            }
            catch (Exception ex)
            {
                LogHandler.LogErrorMessage("NetConfig", "FindNmcliConnection fehlgeschlagen", ex);
                return null;
            }
        }

        private static string? CreateNmcliConnection(string deviceName)
        {
            try
            {
                // Erstelle eine neue Verbindung mit automatischem Namen
                var connectionName = $"neTiPx-{deviceName}";
                
                var psi = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = $"nmcli connection add type ethernet con-name \\\"{connectionName}\\\" ifname {deviceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", "CreateNmcliConnection: Prozess konnte nicht gestartet werden");
                    return null;
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"CreateNmcliConnection fehlgeschlagen: {error}");
                    return null;
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Neue Verbindung erstellt: '{connectionName}' für Device: '{deviceName}'");
                return connectionName;
            }
            catch (Exception ex)
            {
                LogHandler.LogErrorMessage("NetConfig", "CreateNmcliConnection fehlgeschlagen", ex);
                return null;
            }
        }

        private static (bool success, string? error) RunNmcliCommandsElevated(IReadOnlyList<string> commands)
        {
            if (commands.Count == 0)
            {
                return (true, null);
            }

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"nmcli-Befehle starten ({commands.Count} Befehl(e))");
            foreach (var cmd in commands)
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"  CMD: {cmd}");

            try
            {
                // Unter Linux verwenden wir pkexec für grafische Root-Rechte
                // Schreibe alle Befehle in ein temporäres Shell-Skript, um Escaping-Probleme zu vermeiden
                var tempScriptPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"netipx_nmcli_{Guid.NewGuid()}.sh");
                
                try
                {
                    // Schreibe Shell-Skript mit allen Befehlen
                    var scriptContent = "#!/bin/bash\nset -e\n" + string.Join("\n", commands);
                    System.IO.File.WriteAllText(tempScriptPath, scriptContent);
                    
                    // Mache Skript ausführbar
                    var chmodPsi = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{tempScriptPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (var chmodProcess = Process.Start(chmodPsi))
                    {
                        chmodProcess?.WaitForExit();
                    }
                    
                    // Führe Skript mit pkexec aus
                    var psi = new ProcessStartInfo
                    {
                        FileName = "pkexec",
                        Arguments = $"\"{tempScriptPath}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        LogHandler.LogErrorMessage("NetConfig", "Prozess konnte nicht gestartet werden");
                        return (false, "Prozess konnte nicht gestartet werden.");
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        LogHandler.LogErrorMessage("NetConfig", $"nmcli fehlgeschlagen (ExitCode={process.ExitCode}): {error}");
                        return (false, $"Befehl fehlgeschlagen: {error}");
                    }

                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"nmcli output: {output}");
                    }

                    LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", "nmcli-Befehle erfolgreich ausgeführt");
                    return (true, null);
                }
                finally
                {
                    // Lösche temporäres Skript
                    try
                    {
                        if (System.IO.File.Exists(tempScriptPath))
                        {
                            System.IO.File.Delete(tempScriptPath);
                        }
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch (Exception ex)
            {
                LogHandler.LogErrorMessage("NetConfig", "RunNmcliCommandsElevated fehlgeschlagen", ex);
                return (false, "Fehler beim Anwenden: " + ex.Message);
            }
        }

        private static (bool success, string? error) RunShellCommandsElevated(IReadOnlyList<string> commands)
        {
            if (commands.Count == 0)
            {
                return (true, null);
            }

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Shell-Befehle starten ({commands.Count} Befehl(e))");
            foreach (var cmd in commands)
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"  CMD: {cmd}");
            }

            var tempScriptPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"netipx_shell_{Guid.NewGuid()}.sh");
            try
            {
                var scriptContent = "#!/bin/bash\nset -e\n" + string.Join("\n", commands);
                System.IO.File.WriteAllText(tempScriptPath, scriptContent);

                var chmodPsi = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{tempScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var chmodProcess = Process.Start(chmodPsi))
                {
                    chmodProcess?.WaitForExit();
                }

                var psi = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = $"\"{tempScriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, "Prozess konnte nicht gestartet werden.");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    LogHandler.LogErrorMessage("NetConfig", $"Shell-Befehle fehlgeschlagen (ExitCode={process.ExitCode}): {error}");
                    return (false, string.IsNullOrWhiteSpace(error) ? "Shell-Befehle fehlgeschlagen." : error.Trim());
                }

                if (!string.IsNullOrWhiteSpace(output))
                {
                    LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"shell output: {output}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                LogHandler.LogErrorMessage("NetConfig", "RunShellCommandsElevated fehlgeschlagen", ex);
                return (false, "Fehler beim Ausführen von Shell-Befehlen: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (System.IO.File.Exists(tempScriptPath))
                    {
                        System.IO.File.Delete(tempScriptPath);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        public (bool success, List<RouteEntry> routes, string? error) ReadAllPersistentRoutes()
        {
            try
            {
                // Plattformspezifische Implementierung
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return ReadLinuxRoutes();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return ReadWindowsRoutes();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return ReadMacRoutes();
                }
                else
                {
                    return (false, new List<RouteEntry>(), "Plattform wird nicht unterstützt");
                }
            }
            catch (Exception ex)
            {
                return (false, new List<RouteEntry>(), "Fehler beim Einlesen der Routen: " + ex.Message);
            }
        }

        private (bool success, List<RouteEntry> routes, string? error) ReadLinuxRoutes()
        {
            try
            {
                // "ip route show" ausführen
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = "-c \"ip route show\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, new List<RouteEntry>(), "Prozess konnte nicht gestartet werden");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"ip route show fehlgeschlagen: {error}");
                    return (false, new List<RouteEntry>(), $"ip route show Fehler: {error}");
                }

                var routes = ParseLinuxIpRoutes(output);
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Linux Routen gelesen: {routes.Count}");
                
                return (true, routes, null);
            }
            catch (Exception ex)
            {
                LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"ReadLinuxRoutes Exception: {ex.Message}");
                return (false, new List<RouteEntry>(), "Fehler beim Lesen der Linux-Routen: " + ex.Message);
            }
        }

        private List<RouteEntry> ParseLinuxIpRoutes(string output)
        {
            var routes = new List<RouteEntry>();
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                try
                {
                    // Beispielzeilen:
                    // default via 192.168.1.1 dev eth0 proto dhcp metric 100
                    // 192.168.1.0/24 dev eth0 proto kernel scope link src 192.168.1.10 metric 100
                    // 10.0.0.0/8 via 192.168.1.254 dev eth0 metric 200 (keine proto = Benutzerroute)
                    // 10.10.10.10 via 192.168.1.1 dev eth0 metric 1 (keine proto = Benutzerroute)

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    string destination = "0.0.0.0";
                    string subnetMask = "0.0.0.0";
                    string gateway = "0.0.0.0";
                    int metric = 0;
                    string? proto = null;

                    // Destination parsen
                    if (parts[0] == "default")
                    {
                        destination = "0.0.0.0";
                        subnetMask = "0.0.0.0";
                    }
                    else if (parts[0].Contains('/'))
                    {
                        // CIDR-Notation: 192.168.1.0/24
                        var cidrParts = parts[0].Split('/');
                        destination = cidrParts[0];
                        if (cidrParts.Length > 1 && int.TryParse(cidrParts[1], out int prefix))
                        {
                            subnetMask = PrefixLengthToSubnetMask(prefix);
                        }
                    }
                    else
                    {
                        destination = parts[0];
                        subnetMask = "255.255.255.255";
                    }

                    // Gateway, Metric und Proto parsen
                    for (int i = 1; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "via" && i + 1 < parts.Length)
                        {
                            gateway = parts[i + 1];
                        }
                        else if (parts[i] == "metric" && i + 1 < parts.Length)
                        {
                            int.TryParse(parts[i + 1], out metric);
                        }
                        else if (parts[i] == "proto" && i + 1 < parts.Length)
                        {
                            proto = parts[i + 1];
                        }
                    }

                    // Entscheiden, ob die Route löschbar ist:
                    // - kernel, dhcp, boot = Systemrouten (nicht löschbar)
                    // - static, redirect oder kein proto = Benutzerrouten (löschbar)
                    bool canDelete = true;
                    if (proto != null)
                    {
                        var protoLower = proto.ToLower();
                        if (protoLower == "kernel" || protoLower == "dhcp" || protoLower == "boot")
                        {
                            canDelete = false;
                        }
                        // static, redirect oder andere = löschbar
                    }
                    // Keine proto-Angabe = manuell hinzugefügt = löschbar

                    // Route hinzufügen
                    routes.Add(new RouteEntry
                    {
                        Destination = destination,
                        SubnetMask = subnetMask,
                        Gateway = gateway,
                        Metric = metric,
                        CanDeleteFromSystem = canDelete
                    });
                }
                catch (Exception ex)
                {
                    LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"Fehler beim Parsen der Route '{line}': {ex.Message}");
                }
            }

            return routes;
        }

        private (bool success, string? error) AddLinuxRoute(string destination, string subnetMask, string gateway, int metric)
        {
            try
            {
                // Subnetzmaske in CIDR-Präfix umwandeln
                int prefix = SubnetMaskToPrefix(subnetMask);
                if (prefix <= 0)
                {
                    return (false, "Ungültige Subnetzmaske.");
                }

                // ip route add Befehl erstellen
                string command;
                if (destination == "0.0.0.0" && prefix == 0)
                {
                    // Default-Route
                    command = $"ip route add default via {gateway} metric {metric}";
                }
                else
                {
                    // Spezifische Route
                    command = $"ip route add {destination}/{prefix} via {gateway} metric {metric}";
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Linux Route hinzufügen: {command}");

                // Mit pkexec ausführen für Root-Rechte
                var psi = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = $"/bin/sh -c \"{command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, "Prozess konnte nicht gestartet werden.");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"Route hinzufügen fehlgeschlagen: {error}");
                    
                    // Benutzerfreundliche Fehlermeldungen
                    if (error.Contains("File exists") || error.Contains("RTNETLINK answers: File exists"))
                    {
                        return (false, "Route existiert bereits.");
                    }
                    else if (error.Contains("Network is unreachable"))
                    {
                        return (false, "Netzwerk ist nicht erreichbar.");
                    }
                    else if (error.Contains("No route to host"))
                    {
                        return (false, "Keine Route zum Gateway.");
                    }
                    else if (string.IsNullOrWhiteSpace(error))
                    {
                        return (false, "Route konnte nicht hinzugefügt werden.");
                    }
                    
                    return (false, error.Trim());
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", "Route erfolgreich hinzugefügt");
                return (true, null);
            }
            catch (Exception ex)
            {
                LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"AddLinuxRoute Exception: {ex.Message}");
                return (false, $"Fehler beim Hinzufügen der Route: {ex.Message}");
            }
        }

        private (bool success, string? error) DeleteLinuxRoute(string destination, string subnetMask, string gateway)
        {
            try
            {
                // Subnetzmaske in CIDR-Präfix umwandeln
                int prefix = SubnetMaskToPrefix(subnetMask);
                if (prefix <= 0)
                {
                    return (false, "Ungültige Subnetzmaske.");
                }

                // Gateway kann "_gateway" sein (Linux-Platzhalter) oder eine IP-Adresse
                // Bei "_gateway" sollten wir es so belassen, da ip route das versteht
                
                // ip route del Befehl erstellen
                string command;
                if (destination == "0.0.0.0" && prefix == 0)
                {
                    // Default-Route
                    if (!string.IsNullOrWhiteSpace(gateway) && gateway != "0.0.0.0")
                    {
                        command = $"ip route del default via {gateway}";
                    }
                    else
                    {
                        command = "ip route del default";
                    }
                }
                else
                {
                    // Spezifische Route
                    if (!string.IsNullOrWhiteSpace(gateway) && gateway != "0.0.0.0")
                    {
                        command = $"ip route del {destination}/{prefix} via {gateway}";
                    }
                    else
                    {
                        // Route ohne Gateway (direkt verbundenes Netzwerk)
                        command = $"ip route del {destination}/{prefix}";
                    }
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Linux Route löschen: {command}");

                // Mit pkexec ausführen für Root-Rechte
                var psi = new ProcessStartInfo
                {
                    FileName = "pkexec",
                    Arguments = $"/bin/sh -c \"{command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, "Prozess konnte nicht gestartet werden.");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"Route löschen fehlgeschlagen: {error}");
                    
                    // Benutzerfreundliche Fehlermeldungen
                    if (error.Contains("No such process") || error.Contains("RTNETLINK answers: No such process"))
                    {
                        return (false, "Route existiert nicht.");
                    }
                    else if (string.IsNullOrWhiteSpace(error))
                    {
                        return (false, "Route konnte nicht gelöscht werden.");
                    }
                    
                    return (false, error.Trim());
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", "Route erfolgreich gelöscht");
                return (true, null);
            }
            catch (Exception ex)
            {
                LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"DeleteLinuxRoute Exception: {ex.Message}");
                return (false, $"Fehler beim Löschen der Route: {ex.Message}");
            }
        }

        private (bool success, List<RouteEntry> routes, string? error) ReadWindowsRoutes()
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
                return (false, new List<RouteEntry>(), "Fehler beim Einlesen der Windows-Routen: " + ex.Message);
            }
        }

        private (bool success, List<RouteEntry> routes, string? error) ReadMacRoutes()
        {
            try
            {
                // "netstat -rn -f inet" ausführen (macOS-Äquivalent zu "route print -4")
                var result = RunProcessCapture("netstat", "-rn -f inet");
                if (!result.success)
                {
                    LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"netstat -rn fehlgeschlagen: {result.error}");
                    return (false, new List<RouteEntry>(), $"netstat -rn Fehler: {result.error}");
                }

                var routes = ParseMacNetstatRoutes(result.output);
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"macOS Routen gelesen: {routes.Count}");

                return (true, routes, null);
            }
            catch (Exception ex)
            {
                LogHandler.LogSystemMessage(LogLevel.ERROR, "NetConfig", $"ReadMacRoutes Exception: {ex.Message}");
                return (false, new List<RouteEntry>(), "Fehler beim Lesen der macOS-Routen: " + ex.Message);
            }
        }

        private List<RouteEntry> ParseMacNetstatRoutes(string output)
        {
            var routes = new List<RouteEntry>();
            var lines = output.Split('\n');
            var headerSeen = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                try
                {
                    // Beispielzeilen (netstat -rn -f inet):
                    // Destination        Gateway            Flags               Netif Expire
                    // default            192.168.1.1        UGScg                 en0
                    // 127                127.0.0.1          UCS                   lo0
                    // 169.254            link#14            UCS                   en0      !
                    // 192.168.1.1/32     link#14            UCS                   en0      !
                    // 192.168.1.1        0:0:c:9f:f3:4d     UHLWIir               en0   1195

                    if (!headerSeen)
                    {
                        headerSeen = line.StartsWith("Destination", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3)
                    {
                        continue;
                    }

                    var rawDestination = parts[0];
                    var rawGateway = parts[1];
                    var flags = parts[2];

                    // Gateways mit MAC-Adresse (z.B. "0:0:c:9f:f3:4d") sind Nachbar-Cache-Einträge
                    // (ARP), keine echten Routen -> überspringen
                    var gatewayIsIp = IsValidIPv4(rawGateway);
                    if (!gatewayIsIp && rawGateway.Contains(':'))
                    {
                        continue;
                    }

                    if (!TryParseMacDestination(rawDestination, out var destination, out var subnetMask))
                    {
                        continue;
                    }

                    // link#N = On-Link-Route (direkt am Interface, ohne Gateway)
                    var gateway = gatewayIsIp ? rawGateway : "0.0.0.0";

                    // Löschbar nur, wenn per Gateway geroutet und statisch (Flag 'S').
                    // Default-Route und Loopback bleiben geschützt (Verlust der Konnektivität).
                    var canDelete = gatewayIsIp
                                    && flags.Contains('S')
                                    && rawDestination != "default"
                                    && !destination.StartsWith("127.", StringComparison.Ordinal);

                    routes.Add(new RouteEntry
                    {
                        Destination = destination,
                        SubnetMask = subnetMask,
                        Gateway = gateway,
                        Metric = 0, // macOS kennt keine Routen-Metrik
                        CanDeleteFromSystem = canDelete
                    });
                }
                catch (Exception ex)
                {
                    LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"Fehler beim Parsen der Route '{line}': {ex.Message}");
                }
            }

            return routes;
        }

        private static bool TryParseMacDestination(string rawDestination, out string destination, out string subnetMask)
        {
            destination = "0.0.0.0";
            subnetMask = "0.0.0.0";

            if (rawDestination == "default")
            {
                return true;
            }

            var prefixLength = -1;
            var addressPart = rawDestination;

            // CIDR-Notation: "192.168.1.0/24" oder verkürzt "10.77.34/23"
            var slashIndex = rawDestination.IndexOf('/');
            if (slashIndex >= 0)
            {
                addressPart = rawDestination[..slashIndex];
                if (!int.TryParse(rawDestination[(slashIndex + 1)..], out prefixLength)
                    || prefixLength < 0 || prefixLength > 32)
                {
                    return false;
                }
            }

            // netstat kürzt Null-Oktette ab: "127" = 127.0.0.0/8, "169.254" = 169.254.0.0/16
            var octets = addressPart.Split('.');
            if (octets.Length == 0 || octets.Length > 4)
            {
                return false;
            }

            foreach (var octet in octets)
            {
                if (!int.TryParse(octet, out var value) || value < 0 || value > 255)
                {
                    return false;
                }
            }

            if (prefixLength < 0)
            {
                // Ohne CIDR-Angabe: Anzahl der Oktette bestimmt das Präfix,
                // vollständige Adresse = Host-Route (/32)
                prefixLength = octets.Length * 8;
            }

            destination = string.Join('.', octets.Concat(Enumerable.Repeat("0", 4 - octets.Length)));
            subnetMask = PrefixLengthToSubnetMask(prefixLength);
            return true;
        }

        private (bool success, string? error) AddMacRoute(string destination, string subnetMask, string gateway)
        {
            // Hinweis: macOS unterstützt keine Routen-Metrik und keine persistenten Routen;
            // die Route gilt bis zum nächsten Neustart
            var command = $"route -n add -net {destination} -netmask {subnetMask} {gateway}";
            return RunMacShellCommandsElevated(new List<string> { command });
        }

        private (bool success, string? error) DeleteMacRoute(string destination, string subnetMask, string gateway)
        {
            var command = IsValidIPv4(gateway)
                ? $"route -n delete -net {destination} -netmask {subnetMask} {gateway}"
                : $"route -n delete -net {destination} -netmask {subnetMask}";
            return RunMacShellCommandsElevated(new List<string> { command });
        }

        private static (bool success, string? error) RunMacShellCommandsElevated(IReadOnlyList<string> commands)
        {
            if (commands.Count == 0)
            {
                return (true, null);
            }

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"macOS Shell-Befehle starten ({commands.Count} Befehl(e))");
            foreach (var cmd in commands)
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"  CMD: {cmd}");
            }

            try
            {
                // osascript zeigt den systemeigenen Admin-Passwort-Dialog (Pendant zu pkexec)
                var shellCommand = string.Join(" && ", commands);
                var escaped = shellCommand.Replace("\\", "\\\\").Replace("\"", "\\\"");
                var appleScript = $"do shell script \"{escaped}\" with administrator privileges";

                var psi = new ProcessStartInfo
                {
                    FileName = "osascript",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-e");
                psi.ArgumentList.Add(appleScript);

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, "Prozess konnte nicht gestartet werden.");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    LogHandler.LogErrorMessage("NetConfig", $"macOS Shell-Befehle fehlgeschlagen (ExitCode={process.ExitCode}): {error}");
                    return (false, string.IsNullOrWhiteSpace(error) ? "Shell-Befehle fehlgeschlagen." : error.Trim());
                }

                if (!string.IsNullOrWhiteSpace(output))
                {
                    LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"shell output: {output}");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                LogHandler.LogErrorMessage("NetConfig", "RunMacShellCommandsElevated fehlgeschlagen", ex);
                return (false, "Fehler beim Ausführen von Shell-Befehlen: " + ex.Message);
            }
        }

        public (bool success, string? error) DeleteRoute(RouteEntry route)
        {
            var destination = route.Destination?.Trim() ?? string.Empty;
            var subnetMask = route.SubnetMask?.Trim() ?? string.Empty;

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"DeleteRoute: {destination} mask {subnetMask} via {route.Gateway}");

            if (!IsValidIPv4(destination))
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"DeleteRoute abgebrochen: Zieladresse '{destination}' ist ungültig");
                return (false, "Zieladresse ist ungültig.");
            }

            if (SubnetMaskToPrefix(subnetMask) <= 0)
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"DeleteRoute abgebrochen: Subnetzmaske '{subnetMask}' ist ungültig");
                return (false, "Subnetzmaske ist ungültig.");
            }

            if (!TryNormalizeSubnetMask(subnetMask, out var normalizedSubnetMask))
            {
                return (false, "Subnetzmaske ist ungültig.");
            }

            // Plattformspezifische Implementierung
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var runtimeDelete = DeleteLinuxRoute(destination, normalizedSubnetMask, route.Gateway);
                var persistentDelete = RemoveLinuxPersistentRoute(destination, normalizedSubnetMask, route.Gateway);

                if (!runtimeDelete.success &&
                    !IsLinuxRouteNotFoundError(runtimeDelete.error) &&
                    !persistentDelete.success)
                {
                    return runtimeDelete;
                }

                if (!persistentDelete.success)
                {
                    return persistentDelete;
                }

                if (!runtimeDelete.success && IsLinuxRouteNotFoundError(runtimeDelete.error))
                {
                    return (true, null);
                }

                return runtimeDelete;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var commands = new List<string>
                {
                    $"route delete {destination} mask {normalizedSubnetMask} {route.Gateway}"
                };
                return RunNetshCommandsElevated(commands);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return DeleteMacRoute(destination, normalizedSubnetMask, route.Gateway);
            }
            else
            {
                return (false, "Plattform wird nicht unterstützt.");
            }
        }

        private static bool IsLinuxRouteNotFoundError(string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            return error.Contains("No such process", StringComparison.OrdinalIgnoreCase)
                   || error.Contains("Route existiert nicht", StringComparison.OrdinalIgnoreCase);
        }

        private (bool success, string? error) RemoveLinuxPersistentRoute(string destination, string subnetMask, string gateway)
        {
            try
            {
                var prefix = SubnetMaskToPrefix(subnetMask);
                if (prefix < 0)
                {
                    return (false, "Subnetzmaske ist ungültig.");
                }

                var targetPrefix = $"{destination}/{prefix}";
                var listResult = RunProcessCapture("nmcli", "-t -f NAME connection show");
                if (!listResult.success)
                {
                    return (false, $"Konnte NM-Verbindungen nicht lesen: {listResult.error}");
                }

                var connectionNames = listResult.output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var changedCommands = new List<string>();
                var removedFromConnections = 0;

                foreach (var connectionName in connectionNames)
                {
                    var routesResult = RunProcessCapture("nmcli", $"-g ipv4.routes connection show \"{EscapeShellDoubleQuoted(connectionName)}\"");
                    if (!routesResult.success)
                    {
                        continue;
                    }

                    var parsedRoutes = ParseNmcliRoutes(routesResult.output);
                    if (parsedRoutes.Count == 0)
                    {
                        continue;
                    }

                    var remaining = new List<string>();
                    var removedForCurrentConnection = false;
                    foreach (var routeSpec in parsedRoutes)
                    {
                        if (NmcliRouteMatches(routeSpec, targetPrefix, gateway))
                        {
                            removedForCurrentConnection = true;
                            continue;
                        }

                        remaining.Add(routeSpec);
                    }

                    if (!removedForCurrentConnection)
                    {
                        continue;
                    }

                    removedFromConnections++;
                    changedCommands.Add($"nmcli con mod \"{EscapeShellDoubleQuoted(connectionName)}\" ipv4.routes \"\"");
                    foreach (var routeSpec in remaining)
                    {
                        changedCommands.Add($"nmcli con mod \"{EscapeShellDoubleQuoted(connectionName)}\" +ipv4.routes \"{EscapeShellDoubleQuoted(routeSpec)}\"");
                    }
                }

                if (changedCommands.Count == 0)
                {
                    LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", "Keine persistente NM-Route zum Entfernen gefunden.");
                    return (true, null);
                }

                var applyResult = RunNmcliCommandsElevated(changedCommands);
                if (!applyResult.success)
                {
                    return applyResult;
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Persistente Route aus {removedFromConnections} NM-Verbindung(en) entfernt.");
                return (true, null);
            }
            catch (Exception ex)
            {
                LogHandler.LogErrorMessage("NetConfig", "RemoveLinuxPersistentRoute fehlgeschlagen", ex);
                return (false, "Fehler beim Entfernen der persistenten Route: " + ex.Message);
            }
        }

        private static List<string> ParseNmcliRoutes(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return new List<string>();
            }

            return output
                .Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(route => !string.IsNullOrWhiteSpace(route))
                .ToList();
        }

        private static bool NmcliRouteMatches(string routeSpec, string targetPrefix, string gateway)
        {
            var parts = routeSpec.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2)
            {
                return false;
            }

            return string.Equals(parts[0], targetPrefix, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(parts[1], gateway, StringComparison.OrdinalIgnoreCase);
        }

        private static (bool success, string output, string? error) RunProcessCapture(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    return (false, string.Empty, "Prozess konnte nicht gestartet werden.");
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return (false, output, string.IsNullOrWhiteSpace(error) ? "Befehl fehlgeschlagen." : error.Trim());
                }

                return (true, output, null);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
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

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"AddRouteStandalone: {sanitizedRoute.Destination} mask {sanitizedRoute.SubnetMask} via {sanitizedRoute.Gateway} metric {sanitizedRoute.Metric}");

            var (isValid, validationError) = ValidateRoute(sanitizedRoute);
            if (!isValid)
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"AddRouteStandalone Validierung fehlgeschlagen: {validationError}");
                return (false, validationError);
            }

            if (!TryNormalizeSubnetMask(sanitizedRoute.SubnetMask, out var normalizedSubnetMask))
            {
                return (false, $"Subnetzmaske '{sanitizedRoute.SubnetMask}' ist ungueltig.");
            }

            // Plattformspezifische Implementierung
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return AddLinuxRoute(sanitizedRoute.Destination, normalizedSubnetMask, sanitizedRoute.Gateway, sanitizedRoute.Metric);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var commands = new List<string>
                {
                    $"route -p add {sanitizedRoute.Destination} mask {normalizedSubnetMask} {sanitizedRoute.Gateway} metric {sanitizedRoute.Metric}"
                };
                return RunNetshCommandsElevated(commands);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return AddMacRoute(sanitizedRoute.Destination, normalizedSubnetMask, sanitizedRoute.Gateway);
            }
            else
            {
                return (false, "Plattform wird nicht unterstützt.");
            }
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
            var allInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => !n.IsReceiveOnly)
                .ToList();

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"FindNetworkInterface: Suche nach '{adapterKey}'");
            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"FindNetworkInterface: {allInterfaces.Count} Adapter verfügbar");

            var found = allInterfaces
                .FirstOrDefault(n => string.Equals(n.Name, adapterKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Description, adapterKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Name + " - " + n.Description, adapterKey, StringComparison.OrdinalIgnoreCase));

            if (found != null)
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"FindNetworkInterface: Gefunden - Name='{found.Name}', Type={found.NetworkInterfaceType}, Speed={found.Speed}");
            }
            else
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", $"FindNetworkInterface: Nicht gefunden - '{adapterKey}'");
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Verfügbare Adapter: {string.Join(", ", allInterfaces.Select(n => $"'{n.Name}'"))}");
            }

            return found;
        }

        private static (bool success, string? error) RunNetshCommandsElevated(IReadOnlyList<string> commands)
        {
            if (commands.Count == 0)
            {
                return (true, null);
            }

            LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"Netsh-Befehle starten ({commands.Count} Befehl(e))");
            foreach (var cmd in commands)
                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", $"  CMD: {cmd}");

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
                    LogHandler.LogErrorMessage("NetConfig", $"Netsh fehlgeschlagen (ExitCode={process?.ExitCode})");
                    return (false, "netsh wurde nicht erfolgreich ausgefuehrt.");
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "NetConfig", "Netsh-Befehle erfolgreich ausgeführt");
                return (true, null);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "NetConfig", "UAC abgebrochen oder Berechtigung verweigert", ex);
                return (false, "Berechtigung erforderlich: Bitte UAC bestaetigen.");
            }
            catch (Exception ex)
            {
                LogHandler.LogErrorMessage("NetConfig", "RunNetshCommandsElevated fehlgeschlagen", ex);
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

