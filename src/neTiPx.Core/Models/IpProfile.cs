using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Repräsentiert ein IP-Profil mit IP-Konfiguration, Gateway, DNS und Routen
/// </summary>
public sealed partial class IpProfile : ObservableObject
{
    [ObservableProperty]
    private string _name = "IP #1";

    [ObservableProperty]
    private string? _adapterName = string.Empty;

    [ObservableProperty]
    private string _mode = "DHCP";

    [ObservableProperty]
    private string _gateway = string.Empty;

    [ObservableProperty]
    private string _dns1 = string.Empty;

    [ObservableProperty]
    private string _dns2 = string.Empty;

    [ObservableProperty]
    private bool _routesEnabled;

    [ObservableProperty]
    private bool _addRoutesOnApply = true;

    [ObservableProperty]
    private string _linkedUncProfileName = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    public ObservableCollection<IpAddressEntry> IpAddresses { get; } = new();
    public ObservableCollection<RouteEntry> Routes { get; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(AdapterName) 
        ? Name 
        : $"{Name} ({AdapterName})";

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnAdapterNameChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }
}
