using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Models;
using neTiPx.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace neTiPx.Views
{
    public partial class ToolsPage : Page
    {
        private readonly ConfigStore _configStore = new ConfigStore();
        public ObservableCollection<PingTarget> PingTargets { get; } = new ObservableCollection<PingTarget>();
        private readonly Dictionary<PingTarget, CancellationTokenSource> _pingTimers = new Dictionary<PingTarget, CancellationTokenSource>();

        public ToolsPage()
        {
            InitializeComponent();
            DataContext = this;

            // Ping-Ziele aus Config laden
            LoadPingTargets();

            // Standardmäßig PING-Panel anzeigen
            if (ToolsNavView.MenuItems.Count > 0)
            {
                ToolsNavView.SelectedItem = ToolsNavView.MenuItems[0];
            }
        }

        private void ToolsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                // Alle Panels ausblenden
                PingPanel.Visibility = Visibility.Collapsed;
                WlanPanel.Visibility = Visibility.Collapsed;
                PlaceholderPanel.Visibility = Visibility.Collapsed;

                // Ausgewähltes Panel anzeigen
                switch (tag)
                {
                    case "Ping":
                        PingPanel.Visibility = Visibility.Visible;
                        break;
                    case "Wlan":
                        WlanPanel.Visibility = Visibility.Visible;
                        break;
                    case "Placeholder":
                        PlaceholderPanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }

        private void LoadPingTargets()
        {
            var values = _configStore.ReadAll();

            if (!values.TryGetValue("Tools.PingCount", out var countValue) || !int.TryParse(countValue, out var count))
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (values.TryGetValue($"Tools.Ping{i}.IP", out var ip) && !string.IsNullOrWhiteSpace(ip))
                {
                    var intervalSeconds = 5;
                    if (values.TryGetValue($"Tools.Ping{i}.Interval", out var intervalStr) && int.TryParse(intervalStr, out var interval))
                    {
                        intervalSeconds = interval;
                    }

                    var pingTarget = new PingTarget
                    {
                        Target = ip,
                        IntervalSeconds = intervalSeconds,
                        ResponseTimeIpv4 = string.Empty,
                        ResponseTimeIpv6 = string.Empty,
                        StatusColorIpv4 = new SolidColorBrush(Colors.Gray),
                        StatusColorIpv6 = new SolidColorBrush(Colors.Gray)
                    };

                    // Bestimme Adresstyp
                    DetermineAddressType(pingTarget);

                    PingTargets.Add(pingTarget);
                    StartPingingAsync(pingTarget);
                }
            }
        }

        private void AddPingTarget_Click(object sender, RoutedEventArgs e)
        {
            var target = NewPingTargetTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            // Prüfen ob bereits vorhanden
            if (PingTargets.Any(p => p.Target.Equals(target, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var intervalSeconds = (int)PingIntervalNumberBox.Value;
            var pingTarget = new PingTarget
            {
                Target = target,
                IntervalSeconds = intervalSeconds,
                ResponseTimeIpv4 = string.Empty,
                ResponseTimeIpv6 = string.Empty,
                StatusColorIpv4 = new SolidColorBrush(Colors.Gray),
                StatusColorIpv6 = new SolidColorBrush(Colors.Gray)
            };

            // Bestimme Adresstyp
            DetermineAddressType(pingTarget);

            PingTargets.Add(pingTarget);
            SavePingTargets();
            StartPingingAsync(pingTarget);

            NewPingTargetTextBox.Text = string.Empty;
        }

        private void DeletePingTarget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PingTarget target)
            {
                // Timer stoppen
                if (_pingTimers.TryGetValue(target, out var cts))
                {
                    cts.Cancel();
                    _pingTimers.Remove(target);
                }

                PingTargets.Remove(target);
                SavePingTargets();
            }
        }

        private async void StartPingingAsync(PingTarget target)
        {
            var cts = new CancellationTokenSource();
            _pingTimers[target] = cts;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await ExecutePingAsync(target);
                    await Task.Delay(TimeSpan.FromSeconds(target.IntervalSeconds), cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Timer wurde gestoppt
            }
        }

        private async Task ExecutePingAsync(PingTarget target)
        {
            try
            {
                // IPv4 Ping
                var ipv4Task = PingAsync(target.Target, AddressFamily.InterNetwork);
                // IPv6 Ping
                var ipv6Task = PingAsync(target.Target, AddressFamily.InterNetworkV6);

                await Task.WhenAll(ipv4Task, ipv6Task);

                var ipv4Result = await ipv4Task;
                var ipv6Result = await ipv6Task;

                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdatePingResult(target, ipv4Result, ResponseType.IPv4);
                    UpdatePingResult(target, ipv6Result, ResponseType.IPv6);
                });
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    target.ResponseTimeIpv4 = "Fehler";
                    target.StatusColorIpv4 = new SolidColorBrush(Colors.Red);
                    target.ResponseTimeIpv6 = "Fehler";
                    target.StatusColorIpv6 = new SolidColorBrush(Colors.Red);
                });
            }
        }

        private void DetermineAddressType(PingTarget target)
        {
            if (IPAddress.TryParse(target.Target, out var ipAddress))
            {
                // Eindeutige IP-Adresse erkannt
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    // IPv4-Adresse
                    target.ShowIPv4 = Visibility.Visible;
                    target.ShowIPv6 = Visibility.Collapsed;
                }
                else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // IPv6-Adresse
                    target.ShowIPv4 = Visibility.Collapsed;
                    target.ShowIPv6 = Visibility.Visible;
                }
            }
            else
            {
                // Hostname - beide anzeigen
                target.ShowIPv4 = Visibility.Visible;
                target.ShowIPv6 = Visibility.Visible;
            }
        }

        private async Task<PingReply?> PingAsync(string target, AddressFamily addressFamily)
        {
            try
            {
                using var ping = new Ping();

                // Versuche direkt zu parsen, ob es eine IP-Adresse ist
                if (IPAddress.TryParse(target, out var ipAddress))
                {
                    // Direkte IP-Adresse - prüfe ob sie dem gewünschten AddressFamily entspricht
                    if (ipAddress.AddressFamily == addressFamily)
                    {
                        return await ping.SendPingAsync(ipAddress, 3000);
                    }
                    return null; // Adresse entspricht nicht dem gewünschten AddressFamily
                }

                // Versuche Hostname aufzulösen
                var hostEntry = await Dns.GetHostEntryAsync(target);
                if (hostEntry?.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    return null;
                }

                // Finde eine Adresse mit dem gewünschten AddressFamily
                var address = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == addressFamily);
                if (address == null)
                {
                    return null;
                }

                return await ping.SendPingAsync(address, 3000);
            }
            catch
            {
                return null;
            }
        }

        private enum ResponseType
        {
            IPv4,
            IPv6
        }

        private void UpdatePingResult(PingTarget target, PingReply? reply, ResponseType type)
        {
            if (reply != null && reply.Status == IPStatus.Success)
            {
                var responseTimeStr = $"{reply.RoundtripTime} ms";

                // Ampel-Farbe basierend auf Antwortzeit
                var statusColor = reply.RoundtripTime switch
                {
                    < 50 => new SolidColorBrush(Colors.Green),    // Grün
                    < 150 => new SolidColorBrush(Colors.Yellow),  // Gelb
                    _ => new SolidColorBrush(Colors.Orange)       // Orange
                };

                if (type == ResponseType.IPv4)
                {
                    target.ResponseTimeIpv4 = responseTimeStr;
                    target.StatusColorIpv4 = statusColor;
                    target.PingCountIpv4++;
                    target.AddResponseTimeIpv4(reply.RoundtripTime);
                }
                else
                {
                    target.ResponseTimeIpv6 = responseTimeStr;
                    target.StatusColorIpv6 = statusColor;
                    target.PingCountIpv6++;
                    target.AddResponseTimeIpv6(reply.RoundtripTime);
                }
            }
            else
            {
                var statusColor = new SolidColorBrush(Colors.Red); // Rot für Fehler/Timeout

                if (type == ResponseType.IPv4)
                {
                    target.ResponseTimeIpv4 = "Timeout";
                    target.StatusColorIpv4 = statusColor;
                    target.PingCountIpv4++;
                    target.TimeoutCountIpv4++;
                }
                else
                {
                    target.ResponseTimeIpv6 = "Timeout";
                    target.StatusColorIpv6 = statusColor;
                    target.PingCountIpv6++;
                    target.TimeoutCountIpv6++;
                }
            }
        }

        private void SavePingTargets()
        {
            var values = _configStore.ReadAll();

            // Alte Ping-Einträge entfernen
            var oldKeys = values.Keys.Where(k => k.StartsWith("Tools.Ping", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in oldKeys)
            {
                values.Remove(key);
            }

            // Neue Einträge speichern
            values["Tools.PingCount"] = PingTargets.Count.ToString();
            for (int i = 0; i < PingTargets.Count; i++)
            {
                values[$"Tools.Ping{i}.IP"] = PingTargets[i].Target;
                values[$"Tools.Ping{i}.Interval"] = PingTargets[i].IntervalSeconds.ToString();
            }

            _configStore.WriteAll(values);
        }
    }
}
