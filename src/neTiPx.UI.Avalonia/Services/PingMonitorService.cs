using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using neTiPx.UI.Avalonia.Models;

namespace neTiPx.UI.Avalonia.Services;

public sealed class PingMonitorService : IDisposable
{
    private const string StatusDnsError = "DNS Fehler";
    private const string StatusNoIpv4 = "IPv4 nicht vorhanden";
    private const string StatusNoIpv6 = "IPv6 nicht vorhanden";
    private const string StatusTimeout = "Timeout";
    private const string StatusHostUnreachable = "Host unreachable";
    private const string StatusNetworkError = "Netzwerkfehler";
    private const string StatusAccessDenied = "Zugriff verweigert";
    private const string StatusUnknownError = "Unbekannter Fehler";

    private readonly PingMonitorStore _store;
    private readonly SettingsService _settingsService;
    private readonly ObservableCollection<PingMonitorItem> _items = new();
    private readonly Dictionary<Guid, RuntimeEntry> _runtimeEntries = new();
    private readonly object _sync = new();

    private bool _initialized;
    private bool _uiActive;

    public ReadOnlyObservableCollection<PingMonitorItem> Items { get; }

    public PingMonitorService(PingMonitorStore store, SettingsService settingsService)
    {
        _store = store;
        _settingsService = settingsService;
        Items = new ReadOnlyObservableCollection<PingMonitorItem>(_items);
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        var loaded = _store.Load();
        var order = 0;
        foreach (var item in loaded)
        {
            item.Order = order++;
            _items.Add(item);
        }

        ApplyMonitoringPolicyToAll();
    }

    public void SetUiActive(bool isActive)
    {
        _uiActive = isActive;
        ApplyMonitoringPolicyToAll();
    }

    public PingMonitorItem AddTarget(string target, int intervalSeconds, bool runInBackground)
    {
        var normalizedTarget = (target ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            throw new ArgumentException("Target must not be empty.", nameof(target));
        }

        if (_items.Any(i => string.Equals(i.Target, normalizedTarget, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Target already exists.", nameof(target));
        }

        var item = new PingMonitorItem
        {
            Id = Guid.NewGuid(),
            Target = normalizedTarget,
            IntervalSeconds = NormalizeInterval(intervalSeconds),
            RunInBackground = runInBackground,
            IsActive = true,
            Order = _items.Count
        };

        _items.Add(item);
        Save();
        ApplyMonitoringPolicy(item);
        return item;
    }

    public void UpdateTarget(PingMonitorItem item, string target, int intervalSeconds, bool runInBackground)
    {
        var normalizedTarget = (target ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            throw new ArgumentException("Target must not be empty.", nameof(target));
        }

        if (_items.Any(i => !ReferenceEquals(i, item)
                            && string.Equals(i.Target, normalizedTarget, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("Target already exists.", nameof(target));
        }

        item.Target = normalizedTarget;
        item.IntervalSeconds = NormalizeInterval(intervalSeconds);
        item.RunInBackground = runInBackground;

        Save();
        ApplyMonitoringPolicy(item);
    }

    public void DeleteTarget(PingMonitorItem item)
    {
        Stop(item);

        _items.Remove(item);
        for (var i = 0; i < _items.Count; i++)
        {
            _items[i].Order = i;
        }

        Save();
    }

    public void Start(PingMonitorItem item)
    {
        item.IsActive = true;
        Save();
        ApplyMonitoringPolicy(item);
    }

    public void Pause(PingMonitorItem item)
    {
        item.IsActive = false;
        Save();
        ApplyMonitoringPolicy(item);
    }

    public void TriggerNow(PingMonitorItem item)
    {
        _ = ProbeOnceAsync(item, CancellationToken.None);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            foreach (var runtime in _runtimeEntries.Values)
            {
                runtime.Cancellation.Cancel();
                runtime.Gate.Dispose();
            }

            _runtimeEntries.Clear();
        }
    }

    private void ApplyMonitoringPolicyToAll()
    {
        foreach (var item in _items)
        {
            ApplyMonitoringPolicy(item);
        }
    }

    private void ApplyMonitoringPolicy(PingMonitorItem item)
    {
        if (!item.IsActive)
        {
            Stop(item);
            return;
        }

        if (item.RunInBackground || _uiActive)
        {
            StartInternal(item);
            return;
        }

        Stop(item);
    }

    private void StartInternal(PingMonitorItem item)
    {
        lock (_sync)
        {
            if (_runtimeEntries.ContainsKey(item.Id))
            {
                return;
            }

            var runtime = new RuntimeEntry(item);
            _runtimeEntries[item.Id] = runtime;
            _ = RunLoopAsync(runtime);
        }

        UpdateOnUi(() => item.IsRunning = true);
    }

    private void Stop(PingMonitorItem item)
    {
        RuntimeEntry? runtime;
        lock (_sync)
        {
            if (!_runtimeEntries.TryGetValue(item.Id, out runtime))
            {
                UpdateOnUi(() => item.IsRunning = false);
                return;
            }

            _runtimeEntries.Remove(item.Id);
        }

        runtime.Cancellation.Cancel();
        runtime.Gate.Dispose();
        UpdateOnUi(() => item.IsRunning = false);
    }

    private async Task RunLoopAsync(RuntimeEntry runtime)
    {
        var token = runtime.Cancellation.Token;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await ProbeOnceAsync(runtime.Item, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                UpdateFamilyError(runtime.Item, AddressFamily.InterNetwork, "🔴", StatusUnknownError);
                UpdateFamilyError(runtime.Item, AddressFamily.InterNetworkV6, "🔴", StatusUnknownError);
            }

            try
            {
                var delay = TimeSpan.FromSeconds(NormalizeInterval(runtime.Item.IntervalSeconds));
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProbeOnceAsync(PingMonitorItem item, CancellationToken token)
    {
        var runtime = GetOrCreateTransientRuntime(item);
        var hasLock = false;

        try
        {
            await runtime.Gate.WaitAsync(token).ConfigureAwait(false);
            hasLock = true;

            UpdateOnUi(() => item.LastCheckedAt = DateTime.Now);

            var target = item.Target.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                SetErrorState(item, StatusDnsError, StatusDnsError);
                return;
            }

            if (IPAddress.TryParse(target, out var ipAddress))
            {
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    UpdateOnUi(() =>
                    {
                        item.TargetTypeLabel = "IPv4";
                        item.ResolvedIpv4 = ipAddress.ToString();
                        item.ResolvedIpv6 = "-";
                    });

                    await ProbeAddressAndUpdateAsync(item, ipAddress, AddressFamily.InterNetwork, token).ConfigureAwait(false);
                    UpdateOnUi(() =>
                    {
                        item.Ipv6Indicator = "🟡";
                        item.Ipv6StatusText = StatusNoIpv6;
                    });
                    return;
                }

                if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    UpdateOnUi(() =>
                    {
                        item.TargetTypeLabel = "IPv6";
                        item.ResolvedIpv4 = "-";
                        item.ResolvedIpv6 = ipAddress.ToString();
                    });

                    await ProbeAddressAndUpdateAsync(item, ipAddress, AddressFamily.InterNetworkV6, token).ConfigureAwait(false);
                    UpdateOnUi(() =>
                    {
                        item.Ipv4Indicator = "🟡";
                        item.Ipv4StatusText = StatusNoIpv4;
                    });
                    return;
                }
            }

            await ProbeDnsTargetAsync(item, target, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (hasLock)
            {
                runtime.Gate.Release();
            }

            if (runtime.IsTransient)
            {
                runtime.Gate.Dispose();
            }
        }
    }

    private async Task ProbeDnsTargetAsync(PingMonitorItem item, string target, CancellationToken token)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(target).ConfigureAwait(false);
        }
        catch
        {
            SetErrorState(item, StatusDnsError, StatusDnsError);
            return;
        }

        var ipv4Addresses = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).ToList();
        var ipv6Addresses = addresses.Where(a => a.AddressFamily == AddressFamily.InterNetworkV6).ToList();

        var ipv4 = ipv4Addresses.FirstOrDefault();
        var ipv6 = ipv6Addresses.FirstOrDefault();

        UpdateOnUi(() =>
        {
            item.TargetTypeLabel = "DNS";
            item.ResolvedIpv4 = ipv4Addresses.Count > 0
                ? string.Join(", ", ipv4Addresses.Select(a => a.ToString()))
                : "-";
            item.ResolvedIpv6 = ipv6Addresses.Count > 0
                ? string.Join(", ", ipv6Addresses.Select(a => a.ToString()))
                : "-";
        });

        if (ipv4 == null)
        {
            UpdateOnUi(() =>
            {
                item.Ipv4Indicator = "🟡";
                item.Ipv4StatusText = StatusNoIpv4;
            });
        }
        else
        {
            await ProbeAddressAndUpdateAsync(item, ipv4, AddressFamily.InterNetwork, token).ConfigureAwait(false);
        }

        if (ipv6 == null)
        {
            UpdateOnUi(() =>
            {
                item.Ipv6Indicator = "🟡";
                item.Ipv6StatusText = StatusNoIpv6;
            });
        }
        else
        {
            await ProbeAddressAndUpdateAsync(item, ipv6, AddressFamily.InterNetworkV6, token).ConfigureAwait(false);
        }
    }

    private async Task ProbeAddressAndUpdateAsync(PingMonitorItem item, IPAddress address, AddressFamily family, CancellationToken token)
    {
        var timeoutMs = 2000;

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, timeoutMs).ConfigureAwait(false);

            switch (reply.Status)
            {
                case IPStatus.Success:
                {
                    var latency = reply.RoundtripTime;
                    var indicator = GetIndicatorForLatency(latency);
                    var statusText = $"{latency} ms";

                    UpdateOnUi(() =>
                    {
                        ApplyFamilyState(item, family, indicator, statusText);
                        item.RegisterProbeSuccess(family, latency);
                        item.AddLatencySample(family, latency);
                        item.LastResponseAt = DateTime.Now;
                        item.LastResponseText = DateTime.Now.ToString("HH:mm:ss");
                        item.LastErrorText = "-";
                        item.LastSuccessLatencyMs = latency;
                    });

                    break;
                }
                case IPStatus.TimedOut:
                    UpdateFamilyError(item, family, "🔴", StatusTimeout);
                    break;
                case IPStatus.DestinationHostUnreachable:
                case IPStatus.DestinationNetworkUnreachable:
                case IPStatus.DestinationUnreachable:
                case IPStatus.BadRoute:
                    UpdateFamilyError(item, family, "🔴", StatusHostUnreachable);
                    break;
                default:
                    UpdateFamilyError(item, family, "🔴", StatusNetworkError);
                    break;
            }
        }
        catch (PingException ex)
        {
            if (ex.Message.Contains("denied", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("verweigert", StringComparison.OrdinalIgnoreCase))
            {
                UpdateFamilyError(item, family, "🔴", StatusAccessDenied);
                return;
            }

            UpdateFamilyError(item, family, "🔴", StatusUnknownError);
        }
        catch (SocketException)
        {
            UpdateFamilyError(item, family, "🔴", StatusNetworkError);
        }
        catch
        {
            UpdateFamilyError(item, family, "🔴", StatusUnknownError);
        }
    }

    private void ApplyFamilyState(PingMonitorItem item, AddressFamily family, string indicator, string statusText)
    {
        if (family == AddressFamily.InterNetwork)
        {
            item.Ipv4Indicator = indicator;
            item.Ipv4StatusText = statusText;
            return;
        }

        item.Ipv6Indicator = indicator;
        item.Ipv6StatusText = statusText;
    }

    private void UpdateFamilyError(PingMonitorItem item, AddressFamily family, string indicator, string errorText)
    {
        UpdateOnUi(() =>
        {
            ApplyFamilyState(item, family, indicator, errorText);
            item.RegisterProbeFailure(family);
            item.LastErrorText = errorText;
        });
    }

    private void SetErrorState(PingMonitorItem item, string ipv4Text, string ipv6Text)
    {
        UpdateOnUi(() =>
        {
            item.TargetTypeLabel = "DNS";
            item.ResolvedIpv4 = "-";
            item.ResolvedIpv6 = "-";
            item.Ipv4Indicator = "🔴";
            item.Ipv6Indicator = "🔴";
            item.Ipv4StatusText = ipv4Text;
            item.Ipv6StatusText = ipv6Text;
            item.LastErrorText = ipv4Text;
        });
    }

    private string GetIndicatorForLatency(long latencyMs)
    {
        var fast = _settingsService.GetPingThresholdFast();
        var normal = _settingsService.GetPingThresholdNormal();

        if (latencyMs <= fast)
        {
            return "🟢";
        }

        if (latencyMs <= normal)
        {
            return "🟡";
        }

        return "🔴";
    }

    private RuntimeEntry GetOrCreateTransientRuntime(PingMonitorItem item)
    {
        lock (_sync)
        {
            if (_runtimeEntries.TryGetValue(item.Id, out var runtime))
            {
                return runtime;
            }
        }

        return RuntimeEntry.CreateTransient(item);
    }

    private void Save()
    {
        _store.Save(_items);
    }

    private static int NormalizeInterval(int seconds)
    {
        return seconds <= 0 ? 1 : seconds;
    }

    private static void UpdateOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    private sealed class RuntimeEntry
    {
        public PingMonitorItem Item { get; }
        public CancellationTokenSource Cancellation { get; }
        public SemaphoreSlim Gate { get; }
        public bool IsTransient { get; }

        public RuntimeEntry(PingMonitorItem item)
        {
            Item = item;
            Cancellation = new CancellationTokenSource();
            Gate = new SemaphoreSlim(1, 1);
            IsTransient = false;
        }

        private RuntimeEntry(PingMonitorItem item, bool transient)
        {
            Item = item;
            Cancellation = new CancellationTokenSource();
            Gate = new SemaphoreSlim(1, 1);
            IsTransient = transient;
        }

        public static RuntimeEntry CreateTransient(PingMonitorItem item)
        {
            return new RuntimeEntry(item, transient: true);
        }
    }
}
