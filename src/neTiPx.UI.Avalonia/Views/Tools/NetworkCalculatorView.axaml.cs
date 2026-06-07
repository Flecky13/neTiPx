using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Linq;
using System.Net;
using System.Numerics;

namespace neTiPx.UI.Avalonia.Views.Tools;

public partial class NetworkCalculatorView : UserControl
{
    private bool _isSyncingInputs;
    private bool _isIpv6Mode;

    public NetworkCalculatorView()
    {
        InitializeComponent();
        
        // Event-Handler registrieren
        Ipv4RadioButton.IsCheckedChanged += IpVersionRadioButton_CheckedChanged;
        Ipv6RadioButton.IsCheckedChanged += IpVersionRadioButton_CheckedChanged;
        
        IpAddressTextBox.TextChanged += IpAddressTextBox_TextChanged;
        SubnetTextBox.TextChanged += SubnetTextBox_TextChanged;
        CidrTextBox.TextChanged += CidrTextBox_TextChanged;
        MaxHostsTextBox.TextChanged += MaxHostsTextBox_TextChanged;
        
        Ipv6AddressTextBox.TextChanged += Ipv6AddressTextBox_TextChanged;
        Ipv6PrefixTextBox.TextChanged += Ipv6PrefixTextBox_TextChanged;
        
        HostsMinusButton.Click += HostsMinusButton_Click;
        HostsPlusButton.Click += HostsPlusButton_Click;
    }

    private void IpVersionRadioButton_CheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (Ipv4RadioButton == null || Ipv6RadioButton == null)
        {
            return;
        }

        _isIpv6Mode = Ipv6RadioButton.IsChecked == true;

        Ipv4InputBorder.IsVisible = !_isIpv6Mode;
        Ipv6InputBorder.IsVisible = _isIpv6Mode;
        Ipv4ResultsBorder.IsVisible = false;
        Ipv6ResultsBorder.IsVisible = false;
        ErrorBorder.IsVisible = false;

        if (_isIpv6Mode)
        {
            TryCalculateIpv6NetworkAuto();
        }
        else
        {
            TryCalculateNetworkAuto();
        }
    }

    private void IpAddressTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateIpScopeIndicator();
        TryCalculateNetworkAuto();
    }

    private void Ipv6AddressTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateIpv6ScopeIndicator();
        TryCalculateIpv6NetworkAuto();
    }

    private void Ipv6PrefixTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        TryCalculateIpv6NetworkAuto();
    }

    private void SubnetTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSyncingInputs)
        {
            return;
        }

        var subnetInput = SubnetTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(subnetInput))
        {
            SetInputText(() =>
            {
                CidrTextBox.Text = string.Empty;
                MaxHostsTextBox.Text = string.Empty;
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
        SetInputText(() =>
        {
            CidrTextBox.Text = prefixLength.ToString();
            MaxHostsTextBox.Text = maxHosts.ToString();
        });

        TryCalculateNetworkAuto();
    }

    private void CidrTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSyncingInputs)
        {
            return;
        }

        var cidrSuffixInput = CidrTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cidrSuffixInput))
        {
            SetInputText(() =>
            {
                SubnetTextBox.Text = string.Empty;
                MaxHostsTextBox.Text = string.Empty;
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
        SetInputText(() =>
        {
            SubnetTextBox.Text = UintToIp(mask).ToString();
            MaxHostsTextBox.Text = maxHosts.ToString();
        });

        TryCalculateNetworkAuto();
    }

    private void MaxHostsTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSyncingInputs)
        {
            return;
        }

        var maxHostsInput = MaxHostsTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(maxHostsInput))
        {
            SetInputText(() =>
            {
                SubnetTextBox.Text = string.Empty;
                CidrTextBox.Text = string.Empty;
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
        SetInputText(() =>
        {
            CidrTextBox.Text = prefixLength.ToString();
            SubnetTextBox.Text = UintToIp(mask).ToString();
            MaxHostsTextBox.Text = CalculateUsableHosts(prefixLength).ToString();
        });

        TryCalculateNetworkAuto();
    }

    private void HostsMinusButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetCurrentPrefix(out var currentPrefix))
        {
            currentPrefix = 24;
        }

        var nextPrefix = Math.Min(32, currentPrefix + 1);
        ApplyPrefixToInputs(nextPrefix);
        TryCalculateNetworkAuto();
    }

    private void HostsPlusButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!TryGetCurrentPrefix(out var currentPrefix))
        {
            currentPrefix = 24;
        }

        var nextPrefix = Math.Max(0, currentPrefix - 1);
        ApplyPrefixToInputs(nextPrefix);
        TryCalculateNetworkAuto();
    }

    private void SetInputText(Action updateAction)
    {
        _isSyncingInputs = true;
        try
        {
            updateAction();
        }
        finally
        {
            _isSyncingInputs = false;
        }
    }

    private bool TryGetCurrentPrefix(out int prefixLength)
    {
        if (TryParseCidrSuffix(CidrTextBox.Text?.Trim() ?? string.Empty, out prefixLength))
        {
            return true;
        }

        if (TryParseSubnetMask(SubnetTextBox.Text?.Trim() ?? string.Empty, out var subnetMask))
        {
            prefixLength = CountBits(subnetMask);
            return true;
        }

        var maxHostsInput = MaxHostsTextBox.Text?.Trim() ?? string.Empty;
        if (long.TryParse(maxHostsInput, out var maxHosts) && maxHosts >= 0)
        {
            return TryGetPrefixFromMaxHosts(maxHosts, out prefixLength);
        }

        prefixLength = 0;
        return false;
    }

    private void ApplyPrefixToInputs(int prefixLength)
    {
        var mask = PrefixToMask(prefixLength);
        var maxHosts = CalculateUsableHosts(prefixLength);

        SetInputText(() =>
        {
            CidrTextBox.Text = prefixLength.ToString();
            SubnetTextBox.Text = UintToIp(mask).ToString();
            MaxHostsTextBox.Text = maxHosts.ToString();
        });
    }

    private void TryCalculateNetworkAuto()
    {
        ErrorBorder.IsVisible = false;

        var ipInput = IpAddressTextBox.Text?.Trim() ?? string.Empty;
        if (!IPAddress.TryParse(ipInput, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            Ipv4ResultsBorder.IsVisible = false;
            return;
        }

        var cidrInput = CidrTextBox.Text?.Trim() ?? string.Empty;
        if (TryParseCidrSuffix(cidrInput, out var prefixFromCidr))
        {
            CalculateFromSuffix(ip.ToString(), prefixFromCidr.ToString());
            return;
        }

        var subnetInput = SubnetTextBox.Text?.Trim() ?? string.Empty;
        if (TryParseSubnetMask(subnetInput, out var subnetMask))
        {
            var prefixFromSubnet = CountBits(subnetMask);
            CalculateFromSuffix(ip.ToString(), prefixFromSubnet.ToString());
            return;
        }

        Ipv4ResultsBorder.IsVisible = false;
    }

    private void TryCalculateIpv6NetworkAuto()
    {
        ErrorBorder.IsVisible = false;

        var addressInput = Ipv6AddressTextBox.Text?.Trim() ?? string.Empty;
        var prefixInput = Ipv6PrefixTextBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(addressInput) || string.IsNullOrWhiteSpace(prefixInput))
        {
            Ipv6ResultsBorder.IsVisible = false;
            return;
        }

        if (!IPAddress.TryParse(addressInput, out var ipv6Address) || ipv6Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            Ipv6ResultsBorder.IsVisible = false;
            return;
        }

        if (!int.TryParse(prefixInput, out var prefixLength) || prefixLength < 0 || prefixLength > 128)
        {
            Ipv6ResultsBorder.IsVisible = false;
            return;
        }

        try
        {
            CalculateIpv6Network(ipv6Address, prefixLength);
        }
        catch
        {
            Ipv6ResultsBorder.IsVisible = false;
        }
    }

    private void UpdateIpv6ScopeIndicator()
    {
        var input = Ipv6AddressTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            Ipv6ScopeTextBlock.Text = "IP-Bereich: -";
            return;
        }

        if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            Ipv6ScopeTextBlock.Text = "IP-Bereich: Ungültige IPv6-Adresse";
            return;
        }

        Ipv6ScopeTextBlock.Text = $"IP-Bereich: {GetIpv6ScopeLabel(ip)}";
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
            var count = BigInteger.Pow(2, hostBits);
            addressCount = count.ToString("N0");
        }

        NetworkAddressIpv6.Text = networkAddress.ToString();
        PrefixLengthIpv6.Text = $"/{prefixLength}";
        FirstAddressIpv6.Text = firstAddress.ToString();
        LastAddressIpv6.Text = lastAddress.ToString();
        AddressCountIpv6.Text = addressCount;

        Ipv6ResultsBorder.IsVisible = true;
    }

    private string GetIpv6ScopeLabel(IPAddress ipAddress)
    {
        var bytes = ipAddress.GetAddressBytes();

        if (bytes.All(b => b == 0))
        {
            return "Unspezifiziert";
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
            return "Unique Local";
        }

        if (bytes[0] == 0xFF)
        {
            return "Multicast";
        }

        if (bytes.Take(10).All(b => b == 0) && bytes[10] == 0xFF && bytes[11] == 0xFF)
        {
            return "IPv4-Mapped";
        }

        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
        {
            return "Dokumentation";
        }

        if ((bytes[0] & 0xE0) == 0x20)
        {
            return "Global Unicast";
        }

        return "Reserviert";
    }

    private void UpdateIpScopeIndicator()
    {
        var input = IpAddressTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            IpScopeTextBlock.Text = "IP-Bereich: -";
            return;
        }

        if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            IpScopeTextBlock.Text = "IP-Bereich: Ungültige IPv4-Adresse";
            return;
        }

        IpScopeTextBlock.Text = $"IP-Bereich: {GetIpv4ScopeLabel(ip)}";
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
            return "Unspezifiziert";
        }

        if (IsInIpv4Range(ip, 0x7F000000u, 8))
        {
            return "Loopback";
        }

        if (IsInIpv4Range(ip, 0xA9FE0000u, 16))
        {
            return "Link-Local (Zeroconf)";
        }

        if (IsInIpv4Range(ip, 0xE0000000u, 4))
        {
            return "Multicast";
        }

        if (IsInIpv4Range(ip, 0x0A000000u, 8) ||
            IsInIpv4Range(ip, 0xAC100000u, 12) ||
            IsInIpv4Range(ip, 0xC0A80000u, 16))
        {
            return "Privat";
        }

        if (IsInIpv4Range(ip, 0x64400000u, 10))
        {
            return "Shared Address Space (CGNAT)";
        }

        if (IsInIpv4Range(ip, 0xC0000200u, 24) ||
            IsInIpv4Range(ip, 0xC6336400u, 24) ||
            IsInIpv4Range(ip, 0xCB007100u, 24))
        {
            return "Dokumentation";
        }

        if (IsInIpv4Range(ip, 0xF0000000u, 4))
        {
            return "Reserviert";
        }

        return "Öffentlich";
    }

    private bool IsInIpv4Range(uint ip, uint network, int prefixLength)
    {
        var mask = PrefixToMask(prefixLength);
        return (ip & mask) == (network & mask);
    }

    private void CalculateFromSuffix(string ipAddress, string cidrSuffix)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            ShowError("Ungültige IP-Adresse");
            return;
        }

        if (!TryParseCidrSuffix(cidrSuffix, out var prefixLength))
        {
            ShowError("Ungültiges CIDR-Suffix");
            return;
        }

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

        Ipv4ResultsBorder.IsVisible = true;
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
        if (sanitized.StartsWith("/"))
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
        ErrorMessage.Text = message;
        ErrorBorder.IsVisible = true;
        Ipv4ResultsBorder.IsVisible = false;
        Ipv6ResultsBorder.IsVisible = false;
    }
}
