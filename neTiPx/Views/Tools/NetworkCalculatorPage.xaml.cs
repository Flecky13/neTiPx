using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Services;
using System;
using System.Linq;
using System.Net;

namespace neTiPx.Views
{
    public sealed partial class NetworkCalculatorPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private bool _isSyncingNetworkCalcInputs;
        private bool _isIpv6Mode;

        public NetworkCalculatorPage()
        {
            InitializeComponent();
            Loaded += NetworkCalculatorPage_Loaded;
            Unloaded += NetworkCalculatorPage_Unloaded;
        }

        private static string T(string key)
        {
            return _lm.Lang(key);
        }

        private void NetworkCalculatorPage_Loaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _lm.LanguageChanged += OnLanguageChanged;
            UpdateLanguage();
        }

        private void NetworkCalculatorPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            if (NetworkCalcTitleText != null) NetworkCalcTitleText.Text = T("TOOLS_NET_CALC");

            if (NetworkCalcIpAddressLabelText != null) NetworkCalcIpAddressLabelText.Text = T("NETCALC_LABEL_IP_ADDRESS");
            if (NetworkCalcSubnetLabelText != null) NetworkCalcSubnetLabelText.Text = T("NETCALC_LABEL_SUBNET_MASK");
            if (NetworkCalcCidrLabelText != null) NetworkCalcCidrLabelText.Text = T("NETCALC_LABEL_CIDR_SUFFIX");
            if (NetworkCalcMaxHostsLabelText != null) NetworkCalcMaxHostsLabelText.Text = T("NETCALC_LABEL_MAX_HOSTS");

            if (NetworkCalcIpAddressTextBox != null) NetworkCalcIpAddressTextBox.PlaceholderText = T("NETCALC_PLACEHOLDER_IPV4_ADDRESS");
            if (NetworkCalcSubnetTextBox != null) NetworkCalcSubnetTextBox.PlaceholderText = T("NETCALC_PLACEHOLDER_SUBNET_MASK");
            if (NetworkCalcCidrTextBox != null) NetworkCalcCidrTextBox.PlaceholderText = T("NETCALC_PLACEHOLDER_CIDR_SUFFIX");
            if (NetworkCalcMaxHostsTextBox != null) NetworkCalcMaxHostsTextBox.PlaceholderText = T("NETCALC_PLACEHOLDER_MAX_HOSTS");

            if (NetworkCalcHostsMinusButton != null) ToolTipService.SetToolTip(NetworkCalcHostsMinusButton, T("NETCALC_TOOLTIP_HOSTS_MINUS"));
            if (NetworkCalcHostsPlusButton != null) ToolTipService.SetToolTip(NetworkCalcHostsPlusButton, T("NETCALC_TOOLTIP_HOSTS_PLUS"));

            if (NetworkCalcIpv6AddressLabelText != null) NetworkCalcIpv6AddressLabelText.Text = T("NETCALC_LABEL_IPV6_ADDRESS");
            if (NetworkCalcIpv6PrefixLabelText != null) NetworkCalcIpv6PrefixLabelText.Text = T("NETCALC_LABEL_PREFIX_LENGTH");
            if (NetworkCalcIpv6AddressTextBox != null) NetworkCalcIpv6AddressTextBox.PlaceholderText = T("NETCALC_PLACEHOLDER_IPV6_ADDRESS");
            if (NetworkCalcIpv6PrefixTextBox != null) NetworkCalcIpv6PrefixTextBox.PlaceholderText = T("NETCALC_PLACEHOLDER_PREFIX_LENGTH");

            if (NetworkCalcIpv4ResultsTitleText != null) NetworkCalcIpv4ResultsTitleText.Text = T("NETCALC_RESULTS_TITLE");
            if (NetworkCalcIpv6ResultsTitleText != null) NetworkCalcIpv6ResultsTitleText.Text = T("NETCALC_RESULTS_TITLE");
            if (NetworkCalcNetworkAddressLabelText != null) NetworkCalcNetworkAddressLabelText.Text = T("NETCALC_RESULT_NETWORK_ADDRESS");
            if (NetworkCalcBroadcastAddressLabelText != null) NetworkCalcBroadcastAddressLabelText.Text = T("NETCALC_RESULT_BROADCAST_ADDRESS");
            if (NetworkCalcFirstUsableIpLabelText != null) NetworkCalcFirstUsableIpLabelText.Text = T("NETCALC_RESULT_FIRST_USABLE");
            if (NetworkCalcLastUsableIpLabelText != null) NetworkCalcLastUsableIpLabelText.Text = T("NETCALC_RESULT_LAST_USABLE");
            if (NetworkCalcSubnetMaskResultLabelText != null) NetworkCalcSubnetMaskResultLabelText.Text = T("NETCALC_RESULT_SUBNET_MASK");
            if (NetworkCalcCidrResultLabelText != null) NetworkCalcCidrResultLabelText.Text = T("NETCALC_RESULT_CIDR_SUFFIX");
            if (NetworkCalcHostCountLabelText != null) NetworkCalcHostCountLabelText.Text = T("NETCALC_RESULT_HOST_COUNT");
            if (NetworkCalcWildcardMaskLabelText != null) NetworkCalcWildcardMaskLabelText.Text = T("NETCALC_RESULT_WILDCARD_MASK");

            if (NetworkCalcNetworkAddressIpv6LabelText != null) NetworkCalcNetworkAddressIpv6LabelText.Text = T("NETCALC_RESULT_NETWORK_ADDRESS");
            if (NetworkCalcPrefixLengthIpv6LabelText != null) NetworkCalcPrefixLengthIpv6LabelText.Text = T("NETCALC_RESULT_PREFIX_LENGTH");
            if (NetworkCalcFirstAddressIpv6LabelText != null) NetworkCalcFirstAddressIpv6LabelText.Text = T("NETCALC_RESULT_FIRST_ADDRESS");
            if (NetworkCalcLastAddressIpv6LabelText != null) NetworkCalcLastAddressIpv6LabelText.Text = T("NETCALC_RESULT_LAST_ADDRESS");
            if (NetworkCalcAddressCountIpv6LabelText != null) NetworkCalcAddressCountIpv6LabelText.Text = T("NETCALC_RESULT_ADDRESS_COUNT");

            UpdateIpScopeIndicator();
            UpdateIpv6ScopeIndicator();
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
                NetworkCalcIpv6ScopeTextBlock.Text = T("NETCALC_SCOPE_EMPTY");
                return;
            }

            if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                NetworkCalcIpv6ScopeTextBlock.Text = T("NETCALC_SCOPE_INVALID_IPV6");
                return;
            }

            NetworkCalcIpv6ScopeTextBlock.Text = string.Format(T("NETCALC_SCOPE_FORMAT"), GetIpv6ScopeLabel(ip));
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
                addressCount = string.Format(T("NETCALC_IPV6_ADDRESS_COUNT_LARGE"), hostBits);
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
                return T("NETCALC_SCOPE_UNSPECIFIED");
            }

            if (bytes.Take(15).All(b => b == 0) && bytes[15] == 1)
            {
                return T("NETCALC_SCOPE_LOOPBACK");
            }

            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                return T("NETCALC_SCOPE_LINK_LOCAL");
            }

            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return T("NETCALC_SCOPE_UNIQUE_LOCAL");
            }

            if (bytes[0] == 0xFF)
            {
                return T("NETCALC_SCOPE_MULTICAST");
            }

            if (bytes.Take(10).All(b => b == 0) && bytes[10] == 0xFF && bytes[11] == 0xFF)
            {
                return T("NETCALC_SCOPE_IPV4_MAPPED");
            }

            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
            {
                return T("NETCALC_SCOPE_DOCUMENTATION");
            }

            if ((bytes[0] & 0xE0) == 0x20)
            {
                return T("NETCALC_SCOPE_GLOBAL_UNICAST");
            }

            return T("NETCALC_SCOPE_RESERVED");
        }

        private void UpdateIpScopeIndicator()
        {
            var input = NetworkCalcIpAddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                NetworkCalcIpScopeTextBlock.Text = T("NETCALC_SCOPE_EMPTY");
                return;
            }

            if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                NetworkCalcIpScopeTextBlock.Text = T("NETCALC_SCOPE_INVALID_IPV4");
                return;
            }

            NetworkCalcIpScopeTextBlock.Text = string.Format(T("NETCALC_SCOPE_FORMAT"), GetIpv4ScopeLabel(ip));
        }

        private string GetIpv4ScopeLabel(IPAddress ipAddress)
        {
            var octets = ipAddress.GetAddressBytes();
            var ip = (uint)((octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3]);

            if (ip == 0xFFFFFFFFu)
            {
                return T("NETCALC_SCOPE_BROADCAST");
            }

            if (ip == 0u)
            {
                return T("NETCALC_SCOPE_UNSPECIFIED");
            }

            if (IsInIpv4Range(ip, 0x7F000000u, 8))
            {
                return T("NETCALC_SCOPE_LOOPBACK");
            }

            if (IsInIpv4Range(ip, 0xA9FE0000u, 16))
            {
                return T("NETCALC_SCOPE_ZEROCONF");
            }

            if (IsInIpv4Range(ip, 0xE0000000u, 4))
            {
                return T("NETCALC_SCOPE_MULTICAST");
            }

            if (IsInIpv4Range(ip, 0x0A000000u, 8) ||
                IsInIpv4Range(ip, 0xAC100000u, 12) ||
                IsInIpv4Range(ip, 0xC0A80000u, 16))
            {
                return T("NETCALC_SCOPE_PRIVATE");
            }

            if (IsInIpv4Range(ip, 0x64400000u, 10))
            {
                return T("NETCALC_SCOPE_CGNAT");
            }

            if (IsInIpv4Range(ip, 0xC0000200u, 24) ||
                IsInIpv4Range(ip, 0xC6336400u, 24) ||
                IsInIpv4Range(ip, 0xCB007100u, 24))
            {
                return T("NETCALC_SCOPE_DOCUMENTATION");
            }

            if (IsInIpv4Range(ip, 0xF0000000u, 4))
            {
                return T("NETCALC_SCOPE_RESERVED");
            }

            return T("NETCALC_SCOPE_PUBLIC");
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
                    ShowError(T("NETCALC_ERROR_ENTER_IP"));
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
                    ShowError(T("NETCALC_ERROR_ENTER_SUBNET_OR_CIDR"));
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowError(string.Format(T("NETCALC_ERROR_PREFIX"), ex.Message));
            }
        }

        private void CalculateFromSuffix(string ipAddress, string cidrSuffix)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                throw new ArgumentException(T("NETCALC_ERROR_INVALID_IP"));

            if (!TryParseCidrSuffix(cidrSuffix, out var prefixLength))
                throw new ArgumentException(T("NETCALC_ERROR_INVALID_PREFIX"));

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
                throw new ArgumentException(T("NETCALC_ERROR_INVALID_IP"));

            if (!IPAddress.TryParse(subnetMask, out var subnet))
                throw new ArgumentException(T("NETCALC_ERROR_INVALID_SUBNET"));

            uint ipUint = IpToUint(ip);
            uint subnetUint = IpToUint(subnet);
            if (!IsValidSubnetMask(subnetUint))
            {
                throw new ArgumentException(T("NETCALC_ERROR_INVALID_SUBNET_BITS"));
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
            NetworkCalcErrorBar.Title = T("NETCALC_ERROR_TITLE");
            NetworkCalcErrorBar.Message = message;
            NetworkCalcErrorBar.IsOpen = true;
            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
            NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
        }
    }
}
