using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Sockets;
using CommunityToolkit.Mvvm.ComponentModel;

namespace neTiPx.UI.Avalonia.Models;

public partial class PingMonitorItem : ObservableObject
{
    private const int MaxTrendSamples = 30;

    private long _ipv4TotalCount;
    private long _ipv4UnreachableCount;
    private long _ipv4SuccessCount;
    private long _ipv4LatencySum;
    private long? _ipv4MinLatencyMs;
    private long? _ipv4MaxLatencyMs;

    private long _ipv6TotalCount;
    private long _ipv6UnreachableCount;
    private long _ipv6SuccessCount;
    private long _ipv6LatencySum;
    private long? _ipv6MinLatencyMs;
    private long? _ipv6MaxLatencyMs;

    public ObservableCollection<long> Ipv4LatencyHistory { get; } = new();
    public ObservableCollection<long> Ipv6LatencyHistory { get; } = new();

    public PingMonitorItem()
    {
        Ipv4LatencyHistory.CollectionChanged += OnTrendCollectionChanged;
        Ipv6LatencyHistory.CollectionChanged += OnTrendCollectionChanged;
    }

    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    [ObservableProperty]
    private string _target = string.Empty;

    [ObservableProperty]
    private int _intervalSeconds = 5;

    [ObservableProperty]
    private bool _runInBackground = true;

    public string BackgroundModeText => RunInBackground
        ? "Hintergrundtest: aktiv"
        : "Hintergrundtest: aus";

    partial void OnRunInBackgroundChanged(bool value)
    {
        OnPropertyChanged(nameof(BackgroundModeText));
    }

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private int _order;

    [ObservableProperty]
    private bool _isRunning;

    public string RunningStateText => IsRunning ? "Aktiv" : "Pausiert";

    public string StartPauseGlyph => IsRunning ? "⏸️" : "▶️";

    public string StartPauseTooltip => IsRunning ? "Pausieren" : "Starten";

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(RunningStateText));
        OnPropertyChanged(nameof(StartPauseGlyph));
        OnPropertyChanged(nameof(StartPauseTooltip));
    }

    public string Ipv4TrendPath => BuildTrendPath(Ipv4LatencyHistory);
    public string Ipv6TrendPath => BuildTrendPath(Ipv6LatencyHistory);
    public string Ipv4TrendSummary => BuildTrendSummary(Ipv4LatencyHistory);
    public string Ipv6TrendSummary => BuildTrendSummary(Ipv6LatencyHistory);
    public string Ipv4TotalsText => $"Gesamt: {_ipv4TotalCount} / {_ipv4UnreachableCount}";
    public string Ipv6TotalsText => $"Gesamt: {_ipv6TotalCount} / {_ipv6UnreachableCount}";
    public string Ipv4MinAvgMaxText => BuildMinAvgMaxText(_ipv4SuccessCount, _ipv4MinLatencyMs, _ipv4MaxLatencyMs, _ipv4LatencySum);
    public string Ipv6MinAvgMaxText => BuildMinAvgMaxText(_ipv6SuccessCount, _ipv6MinLatencyMs, _ipv6MaxLatencyMs, _ipv6LatencySum);
    public string Ipv4StatusLine => $"{Ipv4Indicator} {Ipv4StatusText}";
    public string Ipv6StatusLine => $"{Ipv6Indicator} {Ipv6StatusText}";

    [ObservableProperty]
    private string _targetTypeLabel = "DNS";

    [ObservableProperty]
    private string _resolvedIpv4 = "-";

    [ObservableProperty]
    private string _resolvedIpv6 = "-";

    [ObservableProperty]
    private string _ipv4Indicator = "🟡";

    partial void OnIpv4IndicatorChanged(string value)
    {
        OnPropertyChanged(nameof(Ipv4StatusLine));
    }

    [ObservableProperty]
    private string _ipv4StatusText = "Nicht getestet";

    partial void OnIpv4StatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(Ipv4StatusLine));
    }

    [ObservableProperty]
    private string _ipv6Indicator = "🟡";

    partial void OnIpv6IndicatorChanged(string value)
    {
        OnPropertyChanged(nameof(Ipv6StatusLine));
    }

    [ObservableProperty]
    private string _ipv6StatusText = "Nicht getestet";

    partial void OnIpv6StatusTextChanged(string value)
    {
        OnPropertyChanged(nameof(Ipv6StatusLine));
    }

    [ObservableProperty]
    private string _lastResponseText = "-";

    [ObservableProperty]
    private string _lastErrorText = "-";

    [ObservableProperty]
    private long? _lastSuccessLatencyMs;

    [ObservableProperty]
    private DateTime? _lastResponseAt;

    [ObservableProperty]
    private DateTime? _lastCheckedAt;

    public string LastCheckedText => LastCheckedAt?.ToString("HH:mm:ss") ?? "-";

    partial void OnLastCheckedAtChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(LastCheckedText));
    }

    public void AddLatencySample(AddressFamily family, long latencyMs)
    {
        if (latencyMs < 0)
        {
            return;
        }

        var list = family == AddressFamily.InterNetwork ? Ipv4LatencyHistory : Ipv6LatencyHistory;
        list.Add(latencyMs);

        while (list.Count > MaxTrendSamples)
        {
            list.RemoveAt(0);
        }
    }

    public void RegisterProbeSuccess(AddressFamily family, long latencyMs)
    {
        if (latencyMs < 0)
        {
            latencyMs = 0;
        }

        if (family == AddressFamily.InterNetwork)
        {
            _ipv4TotalCount++;
            _ipv4SuccessCount++;
            _ipv4LatencySum += latencyMs;
            _ipv4MinLatencyMs = !_ipv4MinLatencyMs.HasValue ? latencyMs : Math.Min(_ipv4MinLatencyMs.Value, latencyMs);
            _ipv4MaxLatencyMs = !_ipv4MaxLatencyMs.HasValue ? latencyMs : Math.Max(_ipv4MaxLatencyMs.Value, latencyMs);
            OnPropertyChanged(nameof(Ipv4TotalsText));
            OnPropertyChanged(nameof(Ipv4MinAvgMaxText));
            return;
        }

        _ipv6TotalCount++;
        _ipv6SuccessCount++;
        _ipv6LatencySum += latencyMs;
        _ipv6MinLatencyMs = !_ipv6MinLatencyMs.HasValue ? latencyMs : Math.Min(_ipv6MinLatencyMs.Value, latencyMs);
        _ipv6MaxLatencyMs = !_ipv6MaxLatencyMs.HasValue ? latencyMs : Math.Max(_ipv6MaxLatencyMs.Value, latencyMs);
        OnPropertyChanged(nameof(Ipv6TotalsText));
        OnPropertyChanged(nameof(Ipv6MinAvgMaxText));
    }

    public void RegisterProbeFailure(AddressFamily family)
    {
        if (family == AddressFamily.InterNetwork)
        {
            _ipv4TotalCount++;
            _ipv4UnreachableCount++;
            OnPropertyChanged(nameof(Ipv4TotalsText));
            OnPropertyChanged(nameof(Ipv4MinAvgMaxText));
            return;
        }

        _ipv6TotalCount++;
        _ipv6UnreachableCount++;
        OnPropertyChanged(nameof(Ipv6TotalsText));
        OnPropertyChanged(nameof(Ipv6MinAvgMaxText));
    }

    private void OnTrendCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Ipv4TrendPath));
        OnPropertyChanged(nameof(Ipv6TrendPath));
        OnPropertyChanged(nameof(Ipv4TrendSummary));
        OnPropertyChanged(nameof(Ipv6TrendSummary));
    }

    private static string BuildTrendPath(ObservableCollection<long> samples)
    {
        if (samples.Count < 2)
        {
            return string.Empty;
        }

        const double width = 220d;
        const double height = 52d;

        var min = samples.Min();
        var max = samples.Max();
        var range = Math.Max(1d, max - min);

        var stepX = width / Math.Max(1, samples.Count - 1);
        var points = samples.Select((value, index) =>
        {
            var x = index * stepX;
            var normalized = (value - min) / range;
            var y = height - (normalized * height);
            return $"{x.ToString("0.##", CultureInfo.InvariantCulture)},{y.ToString("0.##", CultureInfo.InvariantCulture)}";
        }).ToArray();

        if (points.Length == 0)
        {
            return string.Empty;
        }

        return "M " + points[0] + " L " + string.Join(" ", points.Skip(1));
    }

    private static string BuildTrendSummary(ObservableCollection<long> samples)
    {
        if (samples.Count == 0)
        {
            return "Trend: keine Daten";
        }

        var latest = samples[^1];
        var min = samples.Min();
        var max = samples.Max();
        return $"Trend: {latest} ms (min {min} / max {max})";
    }

    private static string BuildMinAvgMaxText(long successCount, long? min, long? max, long sum)
    {
        var avg = successCount > 0 ? Math.Round((double)sum / successCount, 1) : (double?)null;
        var avgText = avg.HasValue ? avg.Value.ToString("0.0", CultureInfo.InvariantCulture) : "-";
        var minText = min.HasValue ? min.Value.ToString(CultureInfo.InvariantCulture) : "-";
        var maxText = max.HasValue ? max.Value.ToString(CultureInfo.InvariantCulture) : "-";
        return $"min: {minText} / avg: {avgText} / max: {maxText}";
    }
}
