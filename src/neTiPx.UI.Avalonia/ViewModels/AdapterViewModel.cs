using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class AdapterViewModel : ObservableObject
{
    private readonly SynchronizationContext? _uiContext;
    private readonly SettingsService _settingsService;
    private readonly UI.Avalonia.Services.NetworkInfoService _networkInfoService;
    private System.Timers.Timer? _pingTimer;
    private bool _isMonitoringActive;
    private CancellationTokenSource? _networkChangeCts;
    
    // Primary Adapter Properties
    [ObservableProperty]
    private string? _primaryAdapterName;
    
    [ObservableProperty]
    private string? _primaryAdapterMac;
    
    [ObservableProperty]
    private string? _primaryAdapterGateway;
    
    [ObservableProperty]
    private string? _primaryAdapterGateway6;
    
    [ObservableProperty]
    private bool _isPrimaryAdapterVisible;
    
    [ObservableProperty]
    private bool _isPrimaryIpv6Available;
    
    [ObservableProperty]
    private bool _isPrimaryAdapterDown;
    
    // Secondary Adapter Properties
    [ObservableProperty]
    private string? _secondaryAdapterName;
    
    [ObservableProperty]
    private string? _secondaryAdapterMac;
    
    [ObservableProperty]
    private string? _secondaryAdapterGateway;
    
    [ObservableProperty]
    private string? _secondaryAdapterGateway6;
    
    [ObservableProperty]
    private bool _isSecondaryAdapterVisible;
    
    [ObservableProperty]
    private bool _isSecondaryIpv6Available;
    
    [ObservableProperty]
    private bool _isSecondaryAdapterDown;
    
    // Connection Status Properties - Primary
    [ObservableProperty]
    private string _gatewayStatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _gatewayPingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _gatewayStatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _dns1StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _dns1PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _dns1StatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _dns2StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _dns2PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _dns2StatusKind = GatewayStatusKind.Unknown;
    
    // IPv6 Connection Status Properties - Primary
    [ObservableProperty]
    private string _gateway6StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _gateway6PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _gateway6StatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _dns1v6StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _dns1v6PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _dns1v6StatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _dns2v6StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _dns2v6PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _dns2v6StatusKind = GatewayStatusKind.Unknown;
    
    // Connection Status Properties - Secondary
    [ObservableProperty]
    private string _secondaryGatewayStatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _secondaryGatewayPingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _secondaryGatewayStatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _secondaryDns1StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _secondaryDns1PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _secondaryDns1StatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _secondaryDns2StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _secondaryDns2PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _secondaryDns2StatusKind = GatewayStatusKind.Unknown;
    
    // IPv6 Connection Status Properties - Secondary
    [ObservableProperty]
    private string _secondaryGateway6StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _secondaryGateway6PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _secondaryGateway6StatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _secondaryDns1v6StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _secondaryDns1v6PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _secondaryDns1v6StatusKind = GatewayStatusKind.Unknown;
    
    [ObservableProperty]
    private string _secondaryDns2v6StatusText = "Unbekannt";
    
    [ObservableProperty]
    private string _secondaryDns2v6PingText = "Ping: -";
    
    [ObservableProperty]
    private GatewayStatusKind _secondaryDns2v6StatusKind = GatewayStatusKind.Unknown;
    
    private string? _selectedAdapterPrimary;
    private string? _selectedAdapterSecondary;
    
    // Collections
    public ObservableCollection<string> PrimaryAdapterIpV4List { get; } = new();
    public ObservableCollection<string> PrimaryAdapterDns4List { get; } = new();
    public ObservableCollection<string> PrimaryAdapterIpV6List { get; } = new();
    public ObservableCollection<string> PrimaryAdapterDns6List { get; } = new();
    
    public ObservableCollection<string> SecondaryAdapterIpV4List { get; } = new();
    public ObservableCollection<string> SecondaryAdapterDns4List { get; } = new();
    public ObservableCollection<string> SecondaryAdapterIpV6List { get; } = new();
    public ObservableCollection<string> SecondaryAdapterDns6List { get; } = new();
    
    public AdapterViewModel()
    {
        _uiContext = SynchronizationContext.Current;
        _settingsService = new SettingsService();
        _networkInfoService = new UI.Avalonia.Services.NetworkInfoService();
        
        LoadSelectionFromConfig();
        RegisterNetworkChangeEvents();
        
        try
        {
            _pingTimer = new System.Timers.Timer(TimeSpan.FromSeconds(1))
            {
                AutoReset = true
            };
            _pingTimer.Elapsed += async (_, _) => await UpdateStatusAsync();
        }
        catch
        {
            // Fehler beim Erstellen des Timers ignorieren
        }
    }
    
    public string? SelectedAdapterPrimary
    {
        get => _selectedAdapterPrimary;
        private set
        {
            if (_selectedAdapterPrimary != value)
            {
                _selectedAdapterPrimary = value;
                UpdatePrimaryAdapterInfo();
                OnPropertyChanged();
            }
        }
    }
    
    public string? SelectedAdapterSecondary
    {
        get => _selectedAdapterSecondary;
        private set
        {
            if (_selectedAdapterSecondary != value)
            {
                _selectedAdapterSecondary = value;
                UpdateSecondaryAdapterInfo();
                OnPropertyChanged();
            }
        }
    }
    
    public string PrimaryDns1Address => PrimaryAdapterDns4List?.Count > 0 ? PrimaryAdapterDns4List[0] : string.Empty;
    public string PrimaryDns2Address => PrimaryAdapterDns4List?.Count > 1 ? PrimaryAdapterDns4List[1] : string.Empty;
    public string SecondaryDns1Address => SecondaryAdapterDns4List?.Count > 0 ? SecondaryAdapterDns4List[0] : string.Empty;
    public string SecondaryDns2Address => SecondaryAdapterDns4List?.Count > 1 ? SecondaryAdapterDns4List[1] : string.Empty;
    
    private void LoadSelectionFromConfig()
    {
        try
        {
            var store = new Core.Services.AdapterStore();
            var settings = store.ReadAdapters();
            
            // Use configured adapters if available
            if (!string.IsNullOrWhiteSpace(settings.PrimaryAdapter))
            {
                SelectedAdapterPrimary = settings.PrimaryAdapter;
            }
            
            if (!string.IsNullOrWhiteSpace(settings.SecondaryAdapter))
            {
                SelectedAdapterSecondary = settings.SecondaryAdapter;
            }
            
            // Fallback: Auto-select first two active adapters if not configured
            if (string.IsNullOrWhiteSpace(SelectedAdapterPrimary) && 
                string.IsNullOrWhiteSpace(SelectedAdapterSecondary))
            {
                var adapters = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .OrderBy(n => n.Name)
                    .ToList();
                
                if (adapters.Count > 0)
                {
                    SelectedAdapterPrimary = adapters[0].Name;
                }
                
                if (adapters.Count > 1)
                {
                    SelectedAdapterSecondary = adapters[1].Name;
                }
            }
        }
        catch
        {
            // Ignore errors, will have empty adapter selection
        }
    }
    
    private void UpdatePrimaryAdapterInfo()
    {
        if (string.IsNullOrEmpty(SelectedAdapterPrimary))
        {
            IsPrimaryAdapterVisible = false;
            IsPrimaryIpv6Available = false;
            ClearPrimaryAdapterInfo();
            return;
        }
        
        var adapter = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.Name == SelectedAdapterPrimary);
        
        if (adapter == null)
        {
            IsPrimaryAdapterVisible = false;
            ClearPrimaryAdapterInfo();
            return;
        }
        
        IsPrimaryAdapterVisible = true;
        PrimaryAdapterName = adapter.Name;
        PrimaryAdapterMac = FormatMacAddress(adapter.GetPhysicalAddress());
        
        if (adapter.OperationalStatus != OperationalStatus.Up)
        {
            IsPrimaryAdapterDown = true;
            IsPrimaryIpv6Available = true;
            PrimaryAdapterIpV4List.Clear();
            PrimaryAdapterIpV4List.Add("Kein Link");
            PrimaryAdapterGateway = string.Empty;
            PrimaryAdapterDns4List.Clear();
            PrimaryAdapterIpV6List.Clear();
            PrimaryAdapterIpV6List.Add("Kein Link");
            PrimaryAdapterGateway6 = string.Empty;
            PrimaryAdapterDns6List.Clear();
            NotifyDnsPropertiesChanged(isPrimary: true);
            return;
        }
        
        IsPrimaryAdapterDown = false;
        var ipProperties = adapter.GetIPProperties();
        
        // Clear collections
        PrimaryAdapterIpV4List.Clear();
        PrimaryAdapterDns4List.Clear();
        PrimaryAdapterIpV6List.Clear();
        PrimaryAdapterDns6List.Clear();
        
        // IPv4 addresses
        var ipv4Addresses = ipProperties.UnicastAddresses
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        foreach (var addr in ipv4Addresses)
        {
            int prefix = GetCidrPrefix(addr.IPv4Mask);
            PrimaryAdapterIpV4List.Add($"{addr.Address}/{prefix}");
        }
        if (PrimaryAdapterIpV4List.Count == 0)
        {
            PrimaryAdapterIpV4List.Add("Keine IPv4 Adresse");
        }
        
        // Gateway IPv4
        var gateway4 = ipProperties.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
        PrimaryAdapterGateway = gateway4?.Address.ToString() ?? "Kein Gateway";
        
        // DNS4 addresses - use NetworkInfoService to get correct DNS servers on Linux
        var ipv4Config = _networkInfoService.GetIpv4Config(adapter.Name);
        if (ipv4Config != null)
        {
            if (!string.IsNullOrWhiteSpace(ipv4Config.Dns1))
            {
                PrimaryAdapterDns4List.Add(ipv4Config.Dns1);
            }
            if (!string.IsNullOrWhiteSpace(ipv4Config.Dns2))
            {
                PrimaryAdapterDns4List.Add(ipv4Config.Dns2);
            }
        }
        
        // Fallback to .NET API if NetworkInfoService didn't return DNS servers
        if (PrimaryAdapterDns4List.Count == 0)
        {
            var dns4Addresses = ipProperties.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork);
            foreach (var addr in dns4Addresses)
            {
                PrimaryAdapterDns4List.Add(addr.ToString());
            }
        }
        
        if (PrimaryAdapterDns4List.Count == 0)
        {
            PrimaryAdapterDns4List.Add("Kein DNS");
        }
        
        // IPv6 addresses
        var ipv6Addresses = ipProperties.UnicastAddresses
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 
                     && !a.Address.IsIPv6LinkLocal);
        foreach (var addr in ipv6Addresses)
        {
            PrimaryAdapterIpV6List.Add($"{addr.Address}/{addr.PrefixLength}");
        }
        
        var hasIpv6 = PrimaryAdapterIpV6List.Count > 0;
        IsPrimaryIpv6Available = hasIpv6;
        
        if (!hasIpv6)
        {
            PrimaryAdapterIpV6List.Add("Keine IPv6 Adresse");
        }
        
        // Gateway IPv6
        var gateway6 = ipProperties.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetworkV6);
        PrimaryAdapterGateway6 = hasIpv6 
            ? gateway6?.Address.ToString() ?? "Kein Gateway"
            : "Keine IPv6 Adresse";
        
        // DNS6 addresses - use NetworkInfoService to get correct DNS servers on Linux
        if (hasIpv6)
        {
            var dns6List = _networkInfoService.GetIpv6DnsServers(adapter.Name);
            foreach (var dns in dns6List)
            {
                PrimaryAdapterDns6List.Add(dns);
            }
        }
        
        if (PrimaryAdapterDns6List.Count == 0)
        {
            PrimaryAdapterDns6List.Add(hasIpv6 ? "Kein DNS" : "Keine IPv6 Adresse");
        }
        
        NotifyDnsPropertiesChanged(isPrimary: true);
        _ = UpdateStatusAsync();
    }
    
    private void UpdateSecondaryAdapterInfo()
    {
        if (string.IsNullOrEmpty(SelectedAdapterSecondary))
        {
            IsSecondaryAdapterVisible = false;
            IsSecondaryIpv6Available = false;
            ClearSecondaryAdapterInfo();
            return;
        }
        
        var adapter = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(n => n.Name == SelectedAdapterSecondary);
        
        if (adapter == null)
        {
            IsSecondaryAdapterVisible = false;
            ClearSecondaryAdapterInfo();
            return;
        }
        
        IsSecondaryAdapterVisible = true;
        SecondaryAdapterName = adapter.Name;
        SecondaryAdapterMac = FormatMacAddress(adapter.GetPhysicalAddress());
        
        if (adapter.OperationalStatus != OperationalStatus.Up)
        {
            IsSecondaryAdapterDown = true;
            IsSecondaryIpv6Available = true;
            SecondaryAdapterIpV4List.Clear();
            SecondaryAdapterIpV4List.Add("Kein Link");
            SecondaryAdapterGateway = string.Empty;
            SecondaryAdapterDns4List.Clear();
            SecondaryAdapterIpV6List.Clear();
            SecondaryAdapterIpV6List.Add("Kein Link");
            SecondaryAdapterGateway6 = string.Empty;
            SecondaryAdapterDns6List.Clear();
            NotifyDnsPropertiesChanged(isPrimary: false);
            return;
        }
        
        IsSecondaryAdapterDown = false;
        var ipProperties = adapter.GetIPProperties();
        
        // Clear collections
        SecondaryAdapterIpV4List.Clear();
        SecondaryAdapterDns4List.Clear();
        SecondaryAdapterIpV6List.Clear();
        SecondaryAdapterDns6List.Clear();
        
        // IPv4 addresses
        var ipv4Addresses = ipProperties.UnicastAddresses
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
        foreach (var addr in ipv4Addresses)
        {
            int prefix = GetCidrPrefix(addr.IPv4Mask);
            SecondaryAdapterIpV4List.Add($"{addr.Address}/{prefix}");
        }
        if (SecondaryAdapterIpV4List.Count == 0)
        {
            SecondaryAdapterIpV4List.Add("Keine IPv4 Adresse");
        }
        
        // Gateway IPv4
        var gateway4 = ipProperties.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
        SecondaryAdapterGateway = gateway4?.Address.ToString() ?? "Kein Gateway";
        
        // DNS4 addresses - use NetworkInfoService to get correct DNS servers on Linux
        var ipv4Config = _networkInfoService.GetIpv4Config(adapter.Name);
        if (ipv4Config != null)
        {
            if (!string.IsNullOrWhiteSpace(ipv4Config.Dns1))
            {
                SecondaryAdapterDns4List.Add(ipv4Config.Dns1);
            }
            if (!string.IsNullOrWhiteSpace(ipv4Config.Dns2))
            {
                SecondaryAdapterDns4List.Add(ipv4Config.Dns2);
            }
        }
        
        // Fallback to .NET API if NetworkInfoService didn't return DNS servers
        if (SecondaryAdapterDns4List.Count == 0)
        {
            var dns4Addresses = ipProperties.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork);
            foreach (var addr in dns4Addresses)
            {
                SecondaryAdapterDns4List.Add(addr.ToString());
            }
        }
        
        if (SecondaryAdapterDns4List.Count == 0)
        {
            SecondaryAdapterDns4List.Add("Kein DNS");
        }
        
        // IPv6 addresses
        var ipv6Addresses = ipProperties.UnicastAddresses
            .Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6 
                     && !a.Address.IsIPv6LinkLocal);
        foreach (var addr in ipv6Addresses)
        {
            SecondaryAdapterIpV6List.Add($"{addr.Address}/{addr.PrefixLength}");
        }
        
        var hasIpv6 = SecondaryAdapterIpV6List.Count > 0;
        IsSecondaryIpv6Available = hasIpv6;
        
        if (!hasIpv6)
        {
            SecondaryAdapterIpV6List.Add("Keine IPv6 Adresse");
        }
        
        // Gateway IPv6
        var gateway6 = ipProperties.GatewayAddresses
            .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetworkV6);
        SecondaryAdapterGateway6 = hasIpv6 
            ? gateway6?.Address.ToString() ?? "Kein Gateway"
            : "Keine IPv6 Adresse";
        
        // DNS6 addresses - use NetworkInfoService to get correct DNS servers on Linux
        if (hasIpv6)
        {
            var dns6List = _networkInfoService.GetIpv6DnsServers(adapter.Name);
            foreach (var dns in dns6List)
            {
                SecondaryAdapterDns6List.Add(dns);
            }
        }
        
        if (SecondaryAdapterDns6List.Count == 0)
        {
            SecondaryAdapterDns6List.Add(hasIpv6 ? "Kein DNS" : "Keine IPv6 Adresse");
        }
        
        NotifyDnsPropertiesChanged(isPrimary: false);
        _ = UpdateStatusAsync();
    }
    
    private void ClearPrimaryAdapterInfo()
    {
        PrimaryAdapterName = null;
        PrimaryAdapterMac = null;
        PrimaryAdapterGateway = null;
        PrimaryAdapterGateway6 = null;
        IsPrimaryIpv6Available = false;
        
        PrimaryAdapterIpV4List.Clear();
        PrimaryAdapterDns4List.Clear();
        PrimaryAdapterIpV6List.Clear();
        PrimaryAdapterDns6List.Clear();
        
        NotifyDnsPropertiesChanged(isPrimary: true);
    }
    
    private void ClearSecondaryAdapterInfo()
    {
        SecondaryAdapterName = null;
        SecondaryAdapterMac = null;
        SecondaryAdapterGateway = null;
        SecondaryAdapterGateway6 = null;
        IsSecondaryIpv6Available = false;
        
        SecondaryAdapterIpV4List.Clear();
        SecondaryAdapterDns4List.Clear();
        SecondaryAdapterIpV6List.Clear();
        SecondaryAdapterDns6List.Clear();
        
        NotifyDnsPropertiesChanged(isPrimary: false);
    }
    
    private void NotifyDnsPropertiesChanged(bool isPrimary)
    {
        if (isPrimary)
        {
            OnPropertyChanged(nameof(PrimaryDns1Address));
            OnPropertyChanged(nameof(PrimaryDns2Address));
        }
        else
        {
            OnPropertyChanged(nameof(SecondaryDns1Address));
            OnPropertyChanged(nameof(SecondaryDns2Address));
        }
    }
    
    private async Task UpdateStatusAsync()
    {
        if (!_isMonitoringActive) return;
        
        try
        {
            // Update Primary Adapter Status (IPv4)
            if (IsPrimaryAdapterVisible && !IsPrimaryAdapterDown)
            {
                await UpdateAdapterStatusAsync(
                    PrimaryAdapterGateway,
                    PrimaryAdapterDns4List?.ToList(),
                    (status, ping, kind) => { GatewayStatusText = status; GatewayPingText = ping; GatewayStatusKind = kind; },
                    (status, ping, kind) => { Dns1StatusText = status; Dns1PingText = ping; Dns1StatusKind = kind; },
                    (status, ping, kind) => { Dns2StatusText = status; Dns2PingText = ping; Dns2StatusKind = kind; }
                );
                
                // Update Primary Adapter Status (IPv6)
                if (IsPrimaryIpv6Available)
                {
                    await CheckHostStatusAsync(
                        NormalizeHostAddress(PrimaryAdapterGateway6),
                        (status, ping, kind) => { Gateway6StatusText = status; Gateway6PingText = ping; Gateway6StatusKind = kind; }
                    );
                    
                    // DNS IPv6
                    var dns6List = PrimaryAdapterDns6List?.ToList();
                    if (dns6List != null && dns6List.Count > 0)
                    {
                        await CheckHostStatusAsync(
                            NormalizeHostAddress(dns6List[0]),
                            (status, ping, kind) => { Dns1v6StatusText = status; Dns1v6PingText = ping; Dns1v6StatusKind = kind; }
                        );
                    }
                    if (dns6List != null && dns6List.Count > 1)
                    {
                        await CheckHostStatusAsync(
                            NormalizeHostAddress(dns6List[1]),
                            (status, ping, kind) => { Dns2v6StatusText = status; Dns2v6PingText = ping; Dns2v6StatusKind = kind; }
                        );
                    }
                }
            }
            
            // Update Secondary Adapter Status (IPv4)
            if (IsSecondaryAdapterVisible && !IsSecondaryAdapterDown)
            {
                await UpdateAdapterStatusAsync(
                    SecondaryAdapterGateway,
                    SecondaryAdapterDns4List?.ToList(),
                    (status, ping, kind) => { SecondaryGatewayStatusText = status; SecondaryGatewayPingText = ping; SecondaryGatewayStatusKind = kind; },
                    (status, ping, kind) => { SecondaryDns1StatusText = status; SecondaryDns1PingText = ping; SecondaryDns1StatusKind = kind; },
                    (status, ping, kind) => { SecondaryDns2StatusText = status; SecondaryDns2PingText = ping; SecondaryDns2StatusKind = kind; }
                );
                
                // Update Secondary Adapter Status (IPv6)
                if (IsSecondaryIpv6Available)
                {
                    await CheckHostStatusAsync(
                        NormalizeHostAddress(SecondaryAdapterGateway6),
                        (status, ping, kind) => { SecondaryGateway6StatusText = status; SecondaryGateway6PingText = ping; SecondaryGateway6StatusKind = kind; }
                    );
                    
                    // DNS IPv6
                    var dns6List = SecondaryAdapterDns6List?.ToList();
                    if (dns6List != null && dns6List.Count > 0)
                    {
                        await CheckHostStatusAsync(
                            NormalizeHostAddress(dns6List[0]),
                            (status, ping, kind) => { SecondaryDns1v6StatusText = status; SecondaryDns1v6PingText = ping; SecondaryDns1v6StatusKind = kind; }
                        );
                    }
                    if (dns6List != null && dns6List.Count > 1)
                    {
                        await CheckHostStatusAsync(
                            NormalizeHostAddress(dns6List[1]),
                            (status, ping, kind) => { SecondaryDns2v6StatusText = status; SecondaryDns2v6PingText = ping; SecondaryDns2v6StatusKind = kind; }
                        );
                    }
                }
            }
        }
        catch
        {
            // Ignore errors
        }
    }
    
    private async Task UpdateAdapterStatusAsync(
        string? gatewayAddress,
        System.Collections.Generic.List<string>? dnsAddresses,
        Action<string, string, GatewayStatusKind> postGateway,
        Action<string, string, GatewayStatusKind> postDns1,
        Action<string, string, GatewayStatusKind> postDns2)
    {
        var gateway = NormalizeHostAddress(gatewayAddress);
        var dns1 = NormalizeHostAddress(dnsAddresses?.Count > 0 ? dnsAddresses[0] : string.Empty);
        var dns2 = NormalizeHostAddress(dnsAddresses?.Count > 1 ? dnsAddresses[1] : string.Empty);
        
        await CheckHostStatusAsync(gateway, postGateway);
        await CheckHostStatusAsync(dns1, postDns1);
        await CheckHostStatusAsync(dns2, postDns2);
    }
    
    private async Task CheckHostStatusAsync(string address, Action<string, string, GatewayStatusKind> callback)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            PostToUI(() => callback("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown));
            return;
        }
        
        if (!IPAddress.TryParse(address, out var parsedAddress))
        {
            PostToUI(() => callback("Nicht erreichbar", "Ungültige Adresse", GatewayStatusKind.Bad));
            return;
        }
        
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(parsedAddress, 1000);
            
            if (reply.Status == IPStatus.Success)
            {
                var ms = reply.RoundtripTime;
                
                // Hole die Schwellwerte aus den Einstellungen
                int thresholdFast = _settingsService.GetPingThresholdFast();
                int thresholdNormal = _settingsService.GetPingThresholdNormal();
                
                string statusText;
                GatewayStatusKind statusKind;
                
                if (ms <= thresholdFast)
                {
                    statusText = "Erreichbar";
                    statusKind = GatewayStatusKind.Good;
                }
                else if (ms <= thresholdNormal)
                {
                    statusText = "Langsam";
                    statusKind = GatewayStatusKind.Warning;
                }
                else
                {
                    statusText = "Sehr langsam";
                    statusKind = GatewayStatusKind.Bad;
                }
                
                PostToUI(() => callback(statusText, $"Ping: {ms} ms", statusKind));
            }
            else
            {
                PostToUI(() => callback("Nicht erreichbar", "Timeout", GatewayStatusKind.Bad));
            }
        }
        catch
        {
            PostToUI(() => callback("Nicht erreichbar", "Fehler", GatewayStatusKind.Bad));
        }
    }
    
    private void PostToUI(Action action)
    {
        if (_uiContext == null)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ => action(), null);
        }
    }
    
    public void StartConnectionMonitoring()
    {
        _isMonitoringActive = true;
        if (_pingTimer != null && !_pingTimer.Enabled)
        {
            _pingTimer.Start();
            _ = UpdateStatusAsync();
        }
    }
    
    public void StopConnectionMonitoring()
    {
        _isMonitoringActive = false;
        if (_pingTimer != null && _pingTimer.Enabled)
        {
            _pingTimer.Stop();
        }
    }
    
    public void RegisterNetworkChangeEvents()
    {
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
    }
    
    public void UnregisterNetworkChangeEvents()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _networkChangeCts?.Cancel();
        _networkChangeCts = null;
    }
    
    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        ScheduleAdapterInfoRefresh();
    }
    
    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        ScheduleAdapterInfoRefresh();
    }
    
    private void ScheduleAdapterInfoRefresh()
    {
        _networkChangeCts?.Cancel();
        _networkChangeCts = new CancellationTokenSource();
        var token = _networkChangeCts.Token;
        
        Task.Delay(400, token).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                PostToUI(() =>
                {
                    if (!string.IsNullOrEmpty(SelectedAdapterPrimary))
                        UpdatePrimaryAdapterInfo();
                    if (!string.IsNullOrEmpty(SelectedAdapterSecondary))
                        UpdateSecondaryAdapterInfo();
                });
            }
        }, TaskScheduler.Default);
    }
    
    private static int GetCidrPrefix(IPAddress mask)
    {
        int bits = 0;
        foreach (byte b in mask.GetAddressBytes())
        {
            int v = b;
            while (v != 0)
            {
                bits += v & 1;
                v >>= 1;
            }
        }
        return bits;
    }
    
    private static string NormalizeHostAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return string.Empty;
        
        var candidate = address.Trim();
        
        if (candidate.Contains(','))
            candidate = candidate.Split(',')[0].Trim();
        else if (candidate.Contains(';'))
            candidate = candidate.Split(';')[0].Trim();
        else if (candidate.Contains(' '))
            candidate = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        
        return candidate;
    }
    
    private static string FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return string.Join(":", bytes.Select(b => b.ToString("X2")));
    }
}

public enum GatewayStatusKind
{
    Unknown,
    Good,
    Warning,
    Bad
}
