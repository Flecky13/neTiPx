using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Repräsentiert ein Netzwerk-Gerät (Scan-Ergebnis)
/// </summary>
public sealed partial class NetworkDevice : ObservableObject
{
    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private string _macAddress = string.Empty;

    [ObservableProperty]
    private string _hostname = string.Empty;

    [ObservableProperty]
    private bool _isOnline;

    [ObservableProperty]
    private DateTime _lastSeen = DateTime.Now;

    public ObservableCollection<string> OpenPorts { get; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Hostname) 
        ? IpAddress 
        : $"{Hostname} ({IpAddress})";
}
