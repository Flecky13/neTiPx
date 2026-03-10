using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Net;

namespace neTiPx.Views
{
    public sealed partial class NetworkCalculatorPage : Page
    {
        private bool _isSyncingNetworkCalcInputs;
        private bool _isIpv6Mode;

        public NetworkCalculatorPage()
        {
            InitializeComponent();
        }

        private void NetworkCalcIpVersionRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (NetworkCalcIpv4RadioButton == null || NetworkCalcIpv6RadioButton == null)
            {
                return;
            }

            _isIpv6Mode = NetworkCalcIpv6RadioButton.IsChecked == true;

            NetworkCalcIpv4InputBorder.Visibility = _isIpv6Mode ? Visibility.Collapsed : Visibility.Visible;
            NetworkCalcIpv6InputBorder.Visibility = _isIpv6Mode ? Visibility.Visible : Visibility.Collapsed;
            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
            NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;

            NetworkCalcErrorBar.IsOpen = false;

            if (_isIpv6Mode)
            {
                TryCalculateIpv6NetworkAuto();
            }
            else
            {
                TryCalculateNetworkAuto();
            }
        }

        private void NetworkCalcIpAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateIpScopeIndicator();
            TryCalculateNetworkAuto();
        }

        private void NetworkCalcIpv6AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateIpv6ScopeIndicator();
            TryCalculateIpv6NetworkAuto();
        }

        private void NetworkCalcIpv6PrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TryCalculateIpv6NetworkAuto();
        }

        private void NetworkCalcSubnetTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingNetworkCalcInputs)
            {
                return;
            }

            var subnetInput = NetworkCalcSubnetTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(subnetInput))
            {
                SetNetworkCalcInputText(() =>
                {
                    NetworkCalcCidrTextBox.Text = string.Empty;
                    NetworkCalcMaxHostsTextBox.Text = string.Empty;
                });
                TryCalculateNetworkAuto();
                return;
            }

            if (!TryParseSubnetMask(subnetInput, out var subnetMask))
            {
                return;
            }

            var prefixLength = CountBits(subnetMask);
            var maxHosts = CalculateUsableHosts(prefixLength);
            SetNetworkCalcInputText(() =>
            {
                NetworkCalcCidrTextBox.Text = prefixLength.ToString();
                NetworkCalcMaxHostsTextBox.Text = maxHosts.ToString();
            });

            TryCalculateNetworkAuto();
        }

        private void NetworkCalcCidrTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingNetworkCalcInputs)
            {
                return;
            }

            var cidrSuffixInput = NetworkCalcCidrTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cidrSuffixInput))
            {
                SetNetworkCalcInputText(() =>
                {
                    NetworkCalcSubnetTextBox.Text = string.Empty;
                    NetworkCalcMaxHostsTextBox.Text = string.Empty;
                });
                TryCalculateNetworkAuto();
                return;
            }

            if (!TryParseCidrSuffix(cidrSuffixInput, out var prefixLength))
            {
                return;
            }

            var mask = PrefixToMask(prefixLength);
            var maxHosts = CalculateUsableHosts(prefixLength);
            SetNetworkCalcInputText(() =>
            {
                NetworkCalcSubnetTextBox.Text = UintToIp(mask).ToString();
                NetworkCalcMaxHostsTextBox.Text = maxHosts.ToString();
            });

            TryCalculateNetworkAuto();
        }

        private void NetworkCalcMaxHostsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingNetworkCalcInputs)
            {
                return;
            }

            var maxHostsInput = NetworkCalcMaxHostsTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(maxHostsInput))
            {
                SetNetworkCalcInputText(() =>
                {
                    NetworkCalcSubnetTextBox.Text = string.Empty;
                    NetworkCalcCidrTextBox.Text = string.Empty;
                });
                TryCalculateNetworkAuto();
                return;
            }

            if (!long.TryParse(maxHostsInput, out var maxHosts) || maxHosts < 0)
            {
                return;
            }

            if (!TryGetPrefixFromMaxHosts(maxHosts, out var prefixLength))
            {
                return;
            }

            var mask = PrefixToMask(prefixLength);
            SetNetworkCalcInputText(() =>
            {
                NetworkCalcCidrTextBox.Text = prefixLength.ToString();
                NetworkCalcSubnetTextBox.Text = UintToIp(mask).ToString();
                NetworkCalcMaxHostsTextBox.Text = CalculateUsableHosts(prefixLength).ToString();
            });

            TryCalculateNetworkAuto();
        }

        private void NetworkCalcHostsMinusButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetCurrentPrefix(out var currentPrefix))
            {
                currentPrefix = 24;
            }

            var nextPrefix = Math.Min(32, currentPrefix + 1);
            ApplyPrefixToNetworkCalcInputs(nextPrefix);
            TryCalculateNetworkAuto();
        }

        private void NetworkCalcHostsPlusButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetCurrentPrefix(out var currentPrefix))
            {
                currentPrefix = 24;
            }

            var nextPrefix = Math.Max(0, currentPrefix - 1);
            ApplyPrefixToNetworkCalcInputs(nextPrefix);
            TryCalculateNetworkAuto();
        }

        private void SetNetworkCalcInputText(Action updateAction)
        {
            _isSyncingNetworkCalcInputs = true;
            try
            {
                updateAction();
            }
            finally
            {
                _isSyncingNetworkCalcInputs = false;
            }
        }

        private bool TryGetCurrentPrefix(out int prefixLength)
        {
            if (TryParseCidrSuffix(NetworkCalcCidrTextBox.Text.Trim(), out prefixLength))
            {
                return true;
            }

            if (TryParseSubnetMask(NetworkCalcSubnetTextBox.Text.Trim(), out var subnetMask))
            {
                prefixLength = CountBits(subnetMask);
                return true;
            }

            var maxHostsInput = NetworkCalcMaxHostsTextBox.Text.Trim();
            if (long.TryParse(maxHostsInput, out var maxHosts) && maxHosts >= 0)
            {
                return TryGetPrefixFromMaxHosts(maxHosts, out prefixLength);
            }

            prefixLength = 0;
            return false;
        }

        private void ApplyPrefixToNetworkCalcInputs(int prefixLength)
        {
            var mask = PrefixToMask(prefixLength);
            var maxHosts = CalculateUsableHosts(prefixLength);

            SetNetworkCalcInputText(() =>
            {
                NetworkCalcCidrTextBox.Text = prefixLength.ToString();
                NetworkCalcSubnetTextBox.Text = UintToIp(mask).ToString();
                NetworkCalcMaxHostsTextBox.Text = maxHosts.ToString();
            });
        }

        private void TryCalculateNetworkAuto()
        {
            NetworkCalcErrorBar.IsOpen = false;

            if (!IPAddress.TryParse(NetworkCalcIpAddressTextBox.Text.Trim(), out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var cidrInput = NetworkCalcCidrTextBox.Text.Trim();
            if (TryParseCidrSuffix(cidrInput, out var prefixFromCidr))
            {
                CalculateFromSuffix(ip.ToString(), prefixFromCidr.ToString());
                return;
            }

            var subnetInput = NetworkCalcSubnetTextBox.Text.Trim();
            if (TryParseSubnetMask(subnetInput, out var subnetMask))
            {
                var prefixFromSubnet = CountBits(subnetMask);
                CalculateFromSuffix(ip.ToString(), prefixFromSubnet.ToString());
                return;
            }

            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
        }

        private void TryCalculateIpv6NetworkAuto()
        {
            NetworkCalcErrorBar.IsOpen = false;

            var addressInput = NetworkCalcIpv6AddressTextBox.Text.Trim();
            var prefixInput = NetworkCalcIpv6PrefixTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(addressInput) || string.IsNullOrWhiteSpace(prefixInput))
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            if (!IPAddress.TryParse(addressInput, out var ipv6Address) || ipv6Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            if (!int.TryParse(prefixInput, out var prefixLength) || prefixLength < 0 || prefixLength > 128)
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                CalculateIpv6Network(ipv6Address, prefixLength);
            }
            catch
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateIpv6ScopeIndicator()
        {
            var input = NetworkCalcIpv6AddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                NetworkCalcIpv6ScopeTextBlock.Text = "IP-Bereich: -";
                return;
            }

            if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                NetworkCalcIpv6ScopeTextBlock.Text = "IP-Bereich: ungültige IPv6-Adresse";
                return;
            }

            NetworkCalcIpv6ScopeTextBlock.Text = $"IP-Bereich: {GetIpv6ScopeLabel(ip)}";
        }

        private void CalculateIpv6Network(IPAddress ipv6Address, int prefixLength)
        {
            var addressBytes = ipv6Address.GetAddressBytes();
            var networkBytes = new byte[16];
            var lastBytes = new byte[16];

            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            for (int i = 0; i < 16; i++)
            {
                if (i < fullBytes)
                {
                    networkBytes[i] = addressBytes[i];
                    lastBytes[i] = addressBytes[i];
                }
                else if (i == fullBytes && remainingBits > 0)
                {
                    byte mask = (byte)(0xFF << (8 - remainingBits));
                    networkBytes[i] = (byte)(addressBytes[i] & mask);
                    lastBytes[i] = (byte)(addressBytes[i] | ~mask);
                }
                else
                {
                    networkBytes[i] = 0;
                    lastBytes[i] = 0xFF;
                }
            }

            var networkAddress = new IPAddress(networkBytes);
            var firstAddress = new IPAddress(networkBytes);
            var lastAddress = new IPAddress(lastBytes);

            var hostBits = 128 - prefixLength;
            string addressCount;
            if (hostBits > 63)
            {
                addressCount = $"2^{hostBits} (sehr groß)";
            }
            else
            {
                var count = System.Numerics.BigInteger.Pow(2, hostBits);
                addressCount = count.ToString("N0");
            }

            NetworkAddressIpv6.Text = networkAddress.ToString();
            PrefixLengthIpv6.Text = $"/{prefixLength}";
            FirstAddressIpv6.Text = firstAddress.ToString();
            LastAddressIpv6.Text = lastAddress.ToString();
            AddressCountIpv6.Text = addressCount;

            NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Visible;
        }

        private string GetIpv6ScopeLabel(IPAddress ipAddress)
        {
            var bytes = ipAddress.GetAddressBytes();

            if (bytes.All(b => b == 0))
            {
                return "Unspecified";
            }

            if (bytes.Take(15).All(b => b == 0) && bytes[15] == 1)
            {
                return "Loopback";
            }

            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                return "Link-Local";
            }

            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return "Unique Local (ULA)";
            }

            if (bytes[0] == 0xFF)
            {
                return "Multicast";
            }

            if (bytes.Take(10).All(b => b == 0) && bytes[10] == 0xFF && bytes[11] == 0xFF)
            {
                return "IPv4-mapped";
            }

            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
            {
                return "Dokumentationsbereich";
            }

            if ((bytes[0] & 0xE0) == 0x20)
            {
                return "Global Unicast";
            }

            return "Reserviert";
        }

        private void UpdateIpScopeIndicator()
        {
            var input = NetworkCalcIpAddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                NetworkCalcIpScopeTextBlock.Text = "IP-Bereich: -";
                return;
            }

            if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                NetworkCalcIpScopeTextBlock.Text = "IP-Bereich: ungültige IPv4-Adresse";
                return;
            }

            NetworkCalcIpScopeTextBlock.Text = $"IP-Bereich: {GetIpv4ScopeLabel(ip)}";
        }

        private string GetIpv4ScopeLabel(IPAddress ipAddress)
        {
            var octets = ipAddress.GetAddressBytes();
            var ip = (uint)((octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3]);

            if (ip == 0xFFFFFFFFu)
            {
                return "Broadcast";
            }

            if (ip == 0u)
            {
                return "Unspecified";
            }

            if (IsInIpv4Range(ip, 0x7F000000u, 8))
            {
                return "Loopback";
            }

            if (IsInIpv4Range(ip, 0xA9FE0000u, 16))
            {
                return "Zeroconf (Link-Local)";
            }

            if (IsInIpv4Range(ip, 0xE0000000u, 4))
            {
                return "Multicast";
            }

            if (IsInIpv4Range(ip, 0x0A000000u, 8) ||
                IsInIpv4Range(ip, 0xAC100000u, 12) ||
                IsInIpv4Range(ip, 0xC0A80000u, 16))
            {
                return "Privater Bereich";
            }

            if (IsInIpv4Range(ip, 0x64400000u, 10))
            {
                return "Shared Address Space (CGNAT)";
            }

            if (IsInIpv4Range(ip, 0xC0000200u, 24) ||
                IsInIpv4Range(ip, 0xC6336400u, 24) ||
                IsInIpv4Range(ip, 0xCB007100u, 24))
            {
                return "Dokumentationsbereich";
            }

            if (IsInIpv4Range(ip, 0xF0000000u, 4))
            {
                return "Reserviert";
            }

            return "Public Bereich";
        }

        private bool IsInIpv4Range(uint ip, uint network, int prefixLength)
        {
            var mask = PrefixToMask(prefixLength);
            return (ip & mask) == (network & mask);
        }

        private void CalculateNetwork_Click(object sender, RoutedEventArgs e)
        {
            NetworkCalcErrorBar.IsOpen = false;
            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;

            try
            {
                string ipAddressInput = NetworkCalcIpAddressTextBox.Text.Trim();
                string subnetInput = NetworkCalcSubnetTextBox.Text.Trim();
                string cidrSuffixInput = NetworkCalcCidrTextBox.Text.Trim();

                if (string.IsNullOrEmpty(ipAddressInput))
                {
                    ShowError("Bitte geben Sie eine IP-Adresse ein.");
                    return;
                }

                if (!string.IsNullOrEmpty(cidrSuffixInput))
                {
                    CalculateFromSuffix(ipAddressInput, cidrSuffixInput);
                }
                else if (!string.IsNullOrEmpty(subnetInput))
                {
                    CalculateFromSubnet(ipAddressInput, subnetInput);
                }
                else
                {
                    ShowError("Bitte geben Sie Subnetzmaske oder CIDR-Sufix ein.");
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Fehler: {ex.Message}");
            }
        }

        private void CalculateFromSuffix(string ipAddress, string cidrSuffix)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                throw new ArgumentException("Ungültige IP-Adresse.");

            if (!TryParseCidrSuffix(cidrSuffix, out var prefixLength))
                throw new ArgumentException("Ungültiges Präfix. Muss zwischen 0 und 32 liegen.");

            uint mask = PrefixToMask(prefixLength);
            uint networkAddress = IpToUint(ip) & mask;
            uint broadcastAddress = networkAddress | ~mask;

            var networkIp = UintToIp(networkAddress);
            var broadcastIp = UintToIp(broadcastAddress);
            var firstUsable = UintToIp(networkAddress + 1);
            var lastUsable = UintToIp(broadcastAddress - 1);
            var subnetMask = UintToIp(mask);
            var wildcard = UintToIp(~mask);

            long hostCount = broadcastAddress - networkAddress - 1;
            if (hostCount < 0) hostCount = 0;

            DisplayResults(networkIp, broadcastIp, firstUsable, lastUsable, subnetMask, wildcard, hostCount, prefixLength);
        }

        private void CalculateFromSubnet(string ipAddress, string subnetMask)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                throw new ArgumentException("Ungültige IP-Adresse.");

            if (!IPAddress.TryParse(subnetMask, out var subnet))
                throw new ArgumentException("Ungültige Subnetzmaske.");

            uint ipUint = IpToUint(ip);
            uint subnetUint = IpToUint(subnet);
            if (!IsValidSubnetMask(subnetUint))
            {
                throw new ArgumentException("Ungültige Subnetzmaske. Die Bits müssen zusammenhängend sein.");
            }

            uint networkAddress = ipUint & subnetUint;
            uint broadcastAddress = networkAddress | ~subnetUint;

            int prefixLength = CountBits(subnetUint);
            var networkIp = UintToIp(networkAddress);
            var broadcastIp = UintToIp(broadcastAddress);
            var firstUsable = UintToIp(networkAddress + 1);
            var lastUsable = UintToIp(broadcastAddress - 1);
            var wildcard = UintToIp(~subnetUint);

            long hostCount = broadcastAddress - networkAddress - 1;
            if (hostCount < 0) hostCount = 0;

            DisplayResults(networkIp, broadcastIp, firstUsable, lastUsable, UintToIp(subnetUint), wildcard, hostCount, prefixLength);
        }

        private void DisplayResults(IPAddress networkAddress, IPAddress broadcastAddress, IPAddress firstUsable,
                                   IPAddress lastUsable, IPAddress subnetMask, IPAddress wildcard, long hostCount, int prefixLength)
        {
            NetworkAddress.Text = networkAddress.ToString();
            BroadcastAddress.Text = broadcastAddress.ToString();
            FirstUsableIp.Text = firstUsable.ToString();
            LastUsableIp.Text = lastUsable.ToString();
            SubnetMask.Text = subnetMask.ToString();
            CidrNotation.Text = $"/{prefixLength}";
            HostCount.Text = hostCount.ToString("N0");
            WildcardMask.Text = wildcard.ToString();

            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Visible;
        }

        private uint IpToUint(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        private uint PrefixToMask(int prefixLength)
        {
            return prefixLength == 0 ? 0u : (0xFFFFFFFFu << (32 - prefixLength));
        }

        private bool TryParseCidrSuffix(string input, out int prefixLength)
        {
            var sanitized = input.Trim();
            if (sanitized.StartsWith("/", StringComparison.Ordinal))
            {
                sanitized = sanitized.Substring(1);
            }

            if (!int.TryParse(sanitized, out prefixLength))
            {
                return false;
            }

            return prefixLength >= 0 && prefixLength <= 32;
        }

        private long CalculateUsableHosts(int prefixLength)
        {
            var hostBits = 32 - prefixLength;
            if (hostBits <= 1)
            {
                return 0;
            }

            return (1L << hostBits) - 2;
        }

        private bool TryGetPrefixFromMaxHosts(long maxHosts, out int prefixLength)
        {
            for (int candidatePrefix = 32; candidatePrefix >= 0; candidatePrefix--)
            {
                var capacity = CalculateUsableHosts(candidatePrefix);
                if (capacity >= maxHosts)
                {
                    prefixLength = candidatePrefix;
                    return true;
                }
            }

            prefixLength = 0;
            return false;
        }

        private bool TryParseSubnetMask(string subnetMask, out uint subnetUint)
        {
            subnetUint = 0;
            if (!IPAddress.TryParse(subnetMask, out var subnet))
            {
                return false;
            }

            subnetUint = IpToUint(subnet);
            return IsValidSubnetMask(subnetUint);
        }

        private bool IsValidSubnetMask(uint subnetMask)
        {
            var seenZero = false;
            for (int i = 31; i >= 0; i--)
            {
                var isOne = ((subnetMask >> i) & 1u) == 1u;
                if (!isOne)
                {
                    seenZero = true;
                    continue;
                }

                if (seenZero)
                {
                    return false;
                }
            }

            return true;
        }

        private IPAddress UintToIp(uint value)
        {
            return new IPAddress(new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            });
        }

        private int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                count += (int)(value & 1);
                value >>= 1;
            }
            return count;
        }

        private void ShowError(string message)
        {
            NetworkCalcErrorBar.Title = "Fehler";
            NetworkCalcErrorBar.Message = message;
            NetworkCalcErrorBar.IsOpen = true;
            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
            NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
        }
    }
}
