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

            return RunNetshCommandsElevated(commands);
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
    }
}
