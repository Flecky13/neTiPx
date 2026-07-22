using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public sealed partial class DesktopOverlayViewModel : ObservableObject
{
    private static readonly LanguageManager Lm = LanguageManager.Instance;
    private static string T(string key) => Lm.Lang(key);

    private readonly InternetService _internetService;
    private readonly AdapterDiscoveryService _adapterDiscoveryService;
    private readonly neTiPx.UI.Avalonia.Services.NetworkInfoService _networkInfoService;
    private readonly DispatcherTimer _tickTimer;
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    private DesktopOverlaySettingsModel _settings = DesktopOverlaySettingsModel.CreateDefault();
    private bool _isRefreshing;
    private DateTime _lastNetworkRefreshUtc = DateTime.MinValue;
    private DateTime _lastExternalIpRefreshUtc = DateTime.MinValue;
    private DateTime _lastRamRefreshUtc = DateTime.MinValue;
    private DateTime _lastUptimeRefreshUtc = DateTime.MinValue;

    public DesktopOverlayViewModel()
    {
        _internetService = new InternetService();
        _adapterDiscoveryService = new AdapterDiscoveryService();
        _networkInfoService = new neTiPx.UI.Avalonia.Services.NetworkInfoService();

        Lines = new ObservableCollection<DesktopOverlayLineViewModel>();

        _tickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += async (_, _) => await RefreshByScheduleAsync();

        ApplySettings(DesktopOverlaySettingsModel.CreateDefault());
    }

    public ObservableCollection<DesktopOverlayLineViewModel> Lines { get; }

    [ObservableProperty]
    private IBrush _textBrush = new SolidColorBrush(Colors.White);

    [ObservableProperty]
    private IBrush _backgroundBrush = new SolidColorBrush(Color.FromArgb(166, 26, 26, 26));

    [ObservableProperty]
    private double _overlayFontSize = 14;

    [ObservableProperty]
    private string _overlayFontFamily = "Consolas";

    [ObservableProperty]
    private Thickness _overlayPadding = new(12);

    [ObservableProperty]
    private Thickness _lineItemMargin = new(0, 0, 0, 4);

    [ObservableProperty]
    private CornerRadius _overlayCornerRadius = new(10);

    [ObservableProperty]
    private BoxShadows _overlayShadow = BoxShadows.Parse("0 6 18 0 #55000000");

    [ObservableProperty]
    private bool _isInteractive;

    [ObservableProperty]
    private bool _isFreeMoveMode;

    public void Start()
    {
        if (!_tickTimer.IsEnabled)
        {
            _tickTimer.Start();
        }
    }

    public void Stop()
    {
        if (_tickTimer.IsEnabled)
        {
            _tickTimer.Stop();
        }
    }

    public void ApplySettings(DesktopOverlaySettingsModel settings)
    {
        _settings = DesktopOverlaySettingsModel.Normalize(settings);

        OverlayFontSize = _settings.FontSize;
        OverlayFontFamily = _settings.FontFamily;
        OverlayPadding = new Thickness(_settings.Padding);
        LineItemMargin = new Thickness(0, 0, 0, _settings.LineSpacing);
        OverlayCornerRadius = new CornerRadius(_settings.CornerRadius);
        OverlayShadow = _settings.ShowShadow
            ? BoxShadows.Parse("0 6 18 0 #55000000")
            : BoxShadows.Parse("0 0 0 0 #00000000");

        TextBrush = new SolidColorBrush(ParseColor(_settings.TextColor, Colors.White));

        var bgBase = ParseColor(_settings.BackgroundColor, Color.Parse("#1A1A1A"));
        var alpha = (byte)Math.Clamp((int)Math.Round((_settings.BackgroundOpacityPercent / 100d) * 255), 0, 255);
        BackgroundBrush = new SolidColorBrush(Color.FromArgb(alpha, bgBase.R, bgBase.G, bgBase.B));

        IsInteractive = _settings.HoverInteractive;
        IsFreeMoveMode = string.Equals(_settings.PositionMode, DesktopOverlayPositionModes.Free, StringComparison.OrdinalIgnoreCase);

        RebuildLines(forceRebuild: true);
    }

    public async Task RefreshNowAsync()
    {
        _lastNetworkRefreshUtc = DateTime.MinValue;
        _lastExternalIpRefreshUtc = DateTime.MinValue;
        _lastRamRefreshUtc = DateTime.MinValue;
        _lastUptimeRefreshUtc = DateTime.MinValue;
        await RefreshByScheduleAsync(force: true);
    }

    private async Task RefreshByScheduleAsync(bool force = false)
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        var anyValueChanged = false;

        try
        {
            var now = DateTime.UtcNow;

            if (force || now - _lastNetworkRefreshUtc >= TimeSpan.FromSeconds(_settings.NetworkRefreshSeconds))
            {
                if (RefreshNetworkValues())
                {
                    anyValueChanged = true;
                }

                _lastNetworkRefreshUtc = now;
            }

            if (force || now - _lastExternalIpRefreshUtc >= TimeSpan.FromMinutes(_settings.ExternalIpRefreshMinutes))
            {
                if (await RefreshExternalIpAsync())
                {
                    anyValueChanged = true;
                }

                _lastExternalIpRefreshUtc = now;
            }

            if (force || now - _lastRamRefreshUtc >= TimeSpan.FromSeconds(_settings.RamRefreshSeconds))
            {
                if (RefreshRamUsage())
                {
                    anyValueChanged = true;
                }

                _lastRamRefreshUtc = now;
            }

            if (force || now - _lastUptimeRefreshUtc >= TimeSpan.FromSeconds(_settings.UptimeRefreshSeconds))
            {
                if (RefreshUptime())
                {
                    anyValueChanged = true;
                }

                _lastUptimeRefreshUtc = now;
            }

            if (anyValueChanged || force)
            {
                RebuildLines(forceRebuild: force);
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private bool RefreshNetworkValues()
    {
        var changed = false;

        changed |= UpdateValue(DesktopOverlayInfoKeys.ComputerName, Environment.MachineName);
        changed |= UpdateValue(DesktopOverlayInfoKeys.User, Environment.UserName);
        changed |= UpdateValue(DesktopOverlayInfoKeys.Domain, GetDomainOrWorkgroup());
        changed |= UpdateValue(DesktopOverlayInfoKeys.OperatingSystem, RuntimeInformation.OSDescription.Trim());

        var activeAdapterName = GetActiveAdapterName();
        changed |= UpdateValue(DesktopOverlayInfoKeys.NetworkAdapter, string.IsNullOrWhiteSpace(activeAdapterName) ? "-" : activeAdapterName);

        if (string.IsNullOrWhiteSpace(activeAdapterName))
        {
            changed |= UpdateValue(DesktopOverlayInfoKeys.IPv4, "-");
            changed |= UpdateValue(DesktopOverlayInfoKeys.IPv6, "-");
            changed |= UpdateValue(DesktopOverlayInfoKeys.Gateway, "-");
            return changed;
        }

        var info = _networkInfoService.GetNetworkInfo(activeAdapterName);
        changed |= UpdateValue(DesktopOverlayInfoKeys.IPv4, FirstLine(GetInfoValue(info, "IPv4")));
        changed |= UpdateValue(DesktopOverlayInfoKeys.IPv6, FirstLine(GetInfoValue(info, "IPv6")));
        changed |= UpdateValue(DesktopOverlayInfoKeys.Gateway, FirstLine(GetInfoValue(info, "Gateway4")));

        return changed;
    }

    private async Task<bool> RefreshExternalIpAsync()
    {
        var externalIp = await _internetService.LoadExternalIpAsync();
        return UpdateValue(DesktopOverlayInfoKeys.ExternalIp, string.IsNullOrWhiteSpace(externalIp) ? "-" : externalIp);
    }

    private bool RefreshUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var formatted = FormatUptime(uptime);
        return UpdateValue(DesktopOverlayInfoKeys.Uptime, formatted);
    }

    private bool RefreshRamUsage()
    {
        if (!TryReadSystemMemory(out var usedBytes, out var totalBytes))
        {
            return UpdateValue(DesktopOverlayInfoKeys.RamUsage, "-");
        }

        var usedGb = usedBytes / 1024d / 1024d / 1024d;
        var totalGb = totalBytes / 1024d / 1024d / 1024d;
        var percent = totalBytes > 0 ? (usedBytes / (double)totalBytes) * 100 : 0;
        var formatted = string.Format(CultureInfo.InvariantCulture, "{0:0.0} GB / {1:0.0} GB ({2:0}%)", usedGb, totalGb, percent);
        return UpdateValue(DesktopOverlayInfoKeys.RamUsage, formatted);
    }

    private static string GetInfoValue(string[,]? info, string key)
    {
        if (info == null)
        {
            return "-";
        }

        var rows = info.GetLength(0);
        for (var i = 0; i < rows; i++)
        {
            var currentKey = info[i, 0] ?? string.Empty;
            if (currentKey.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(info[i, 1]) ? "-" : info[i, 1];
            }
        }

        return "-";
    }

    private static string FirstLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var line = value
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(line) ? "-" : line.Trim();
    }

    private bool UpdateValue(string key, string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        if (_values.TryGetValue(key, out var existing) && string.Equals(existing, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        _values[key] = normalized;
        return true;
    }

    private void RebuildLines(bool forceRebuild = false)
    {
        var orderedItems = _settings.Items.OrderBy(item => item.Order).ToList();
        var next = new List<DesktopOverlayLineViewModel>();

        foreach (var item in orderedItems)
        {
            if (!item.IsVisible)
            {
                continue;
            }

            var value = _values.TryGetValue(item.Key, out var currentValue) ? currentValue : "-";
            var label = GetLabel(item.Key);

            string text;
            if (item.ShowLabel && item.ShowValue)
            {
                text = $"{label}: {value}";
            }
            else if (item.ShowLabel)
            {
                text = label;
            }
            else if (item.ShowValue)
            {
                text = value;
            }
            else
            {
                continue;
            }

            next.Add(new DesktopOverlayLineViewModel(item.Key, text));
        }

        if (!forceRebuild && IsLineSetEqual(next))
        {
            return;
        }

        Lines.Clear();
        foreach (var line in next)
        {
            Lines.Add(line);
        }
    }

    private bool IsLineSetEqual(IReadOnlyList<DesktopOverlayLineViewModel> next)
    {
        if (Lines.Count != next.Count)
        {
            return false;
        }

        for (var i = 0; i < Lines.Count; i++)
        {
            if (!string.Equals(Lines[i].Key, next[i].Key, StringComparison.Ordinal)
                || !string.Equals(Lines[i].Text, next[i].Text, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static Color ParseColor(string? raw, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        try
        {
            return Color.Parse(raw);
        }
        catch
        {
            return fallback;
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        var days = (int)uptime.TotalDays;
        var hours = uptime.Hours;
        var minutes = uptime.Minutes;

        if (days > 0)
        {
            return $"{days}d {hours}h {minutes}m";
        }

        if (hours > 0)
        {
            return $"{hours}h {minutes}m";
        }

        return $"{Math.Max(1, minutes)}m";
    }

    private static string GetDomainOrWorkgroup()
    {
        try
        {
            var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
            if (!string.IsNullOrWhiteSpace(domain))
            {
                return domain;
            }
        }
        catch
        {
            // ignore
        }

        try
        {
            var userDomain = Environment.UserDomainName;
            if (!string.IsNullOrWhiteSpace(userDomain))
            {
                return userDomain;
            }
        }
        catch
        {
            // ignore
        }

        return "-";
    }

    private string GetActiveAdapterName()
    {
        try
        {
            var adapters = _adapterDiscoveryService.GetActiveAdapters();
            if (adapters.Length == 0)
            {
                return string.Empty;
            }

            var withGateway = adapters.FirstOrDefault(adapter =>
            {
                var props = adapter.GetIPProperties();
                return props.GatewayAddresses.Any(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            });

            return withGateway?.Name ?? adapters[0].Name;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetLabel(string key)
    {
        return key switch
        {
            DesktopOverlayInfoKeys.ComputerName => T("OVERLAY_INFO_COMPUTER_NAME"),
            DesktopOverlayInfoKeys.User => T("OVERLAY_INFO_USER"),
            DesktopOverlayInfoKeys.IPv4 => T("OVERLAY_INFO_IPV4"),
            DesktopOverlayInfoKeys.IPv6 => T("OVERLAY_INFO_IPV6"),
            DesktopOverlayInfoKeys.Gateway => T("OVERLAY_INFO_GATEWAY"),
            DesktopOverlayInfoKeys.ExternalIp => T("OVERLAY_INFO_EXTERNAL_IP"),
            DesktopOverlayInfoKeys.Domain => T("OVERLAY_INFO_DOMAIN"),
            DesktopOverlayInfoKeys.OperatingSystem => T("OVERLAY_INFO_OPERATING_SYSTEM"),
            DesktopOverlayInfoKeys.Uptime => T("OVERLAY_INFO_UPTIME"),
            DesktopOverlayInfoKeys.RamUsage => T("OVERLAY_INFO_RAM"),
            DesktopOverlayInfoKeys.NetworkAdapter => T("OVERLAY_INFO_NETWORK_ADAPTER"),
            _ => key
        };
    }

    private static bool TryReadSystemMemory(out long usedBytes, out long totalBytes)
    {
        usedBytes = 0;
        totalBytes = 0;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (TryReadWindowsMemory(out usedBytes, out totalBytes))
            {
                return true;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (TryReadLinuxMemory(out usedBytes, out totalBytes))
            {
                return true;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            if (TryReadMacMemory(out usedBytes, out totalBytes))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadLinuxMemory(out long usedBytes, out long totalBytes)
    {
        usedBytes = 0;
        totalBytes = 0;

        try
        {
            if (!File.Exists("/proc/meminfo"))
            {
                return false;
            }

            long totalKb = 0;
            long availableKb = 0;
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                {
                    totalKb = ParseMemInfoKb(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                {
                    availableKb = ParseMemInfoKb(line);
                }
            }

            if (totalKb <= 0)
            {
                return false;
            }

            totalBytes = totalKb * 1024;
            usedBytes = Math.Max(0, (totalKb - availableKb) * 1024);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static long ParseMemInfoKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return 0;
        }

        return long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static bool TryReadMacMemory(out long usedBytes, out long totalBytes)
    {
        usedBytes = 0;
        totalBytes = 0;

        try
        {
            var totalRaw = RunCommand("sysctl", "-n hw.memsize");
            if (!long.TryParse(totalRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out totalBytes) || totalBytes <= 0)
            {
                return false;
            }

            var vmStatOutput = RunCommand("vm_stat", string.Empty);
            if (string.IsNullOrWhiteSpace(vmStatOutput))
            {
                return false;
            }

            var pageSize = 4096L;
            foreach (var line in vmStatOutput.Split('\n'))
            {
                if (line.Contains("page size of", StringComparison.OrdinalIgnoreCase))
                {
                    var marker = "page size of";
                    var index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        var tail = line[(index + marker.Length)..].Trim();
                        var bytesToken = tail.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        if (long.TryParse(bytesToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPageSize) && parsedPageSize > 0)
                        {
                            pageSize = parsedPageSize;
                        }
                    }
                }
            }

            long freePages = 0;
            long speculativePages = 0;
            foreach (var line in vmStatOutput.Split('\n'))
            {
                if (line.StartsWith("Pages free:", StringComparison.OrdinalIgnoreCase))
                {
                    freePages = ParseVmStatPages(line);
                }
                else if (line.StartsWith("Pages speculative:", StringComparison.OrdinalIgnoreCase))
                {
                    speculativePages = ParseVmStatPages(line);
                }
            }

            var availableBytes = (freePages + speculativePages) * pageSize;
            usedBytes = Math.Max(0, totalBytes - availableBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static long ParseVmStatPages(string line)
    {
        var value = line.Split(':').LastOrDefault()?.Trim().TrimEnd('.');
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string RunCommand(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
        {
            return string.Empty;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(1500);
        return output;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    private static bool TryReadWindowsMemory(out long usedBytes, out long totalBytes)
    {
        usedBytes = 0;
        totalBytes = 0;

        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            return false;
        }

        totalBytes = (long)status.TotalPhys;
        usedBytes = (long)(status.TotalPhys - status.AvailPhys);
        return totalBytes > 0;
    }
}
