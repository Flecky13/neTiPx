using CommunityToolkit.Mvvm.ComponentModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Ping-Ziel für Netzwerk-Überwachung
/// </summary>
public sealed partial class PingTarget : ObservableObject
{
    [ObservableProperty]
    private string _hostOrIp = string.Empty;

    [ObservableProperty]
    private bool _isReachable;

    [ObservableProperty]
    private long _latencyMs;

    [ObservableProperty]
    private DateTime _lastChecked = DateTime.MinValue;

    [ObservableProperty]
    private string _status = "Unknown";

    public string DisplayStatus => IsReachable 
        ? $"Online ({LatencyMs}ms)" 
        : "Offline";
}
