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
using System.Net.NetworkInformation;
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
                        ResponseTime = string.Empty,
                        StatusColor = new SolidColorBrush(Colors.Gray)
                    };

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
                ResponseTime = string.Empty,
                StatusColor = new SolidColorBrush(Colors.Gray)
            };

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
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target.Target, 3000);

                DispatcherQueue.TryEnqueue(() =>
                {
                    if (reply.Status == IPStatus.Success)
                    {
                        target.ResponseTime = $"{reply.RoundtripTime} ms";

                        // Ampel-Farbe basierend auf Antwortzeit
                        if (reply.RoundtripTime < 50)
                        {
                            target.StatusColor = new SolidColorBrush(Colors.Green); // Grün
                        }
                        else if (reply.RoundtripTime < 150)
                        {
                            target.StatusColor = new SolidColorBrush(Colors.Yellow); // Gelb
                        }
                        else
                        {
                            target.StatusColor = new SolidColorBrush(Colors.Orange); // Orange
                        }
                    }
                    else
                    {
                        target.ResponseTime = "Timeout";
                        target.StatusColor = new SolidColorBrush(Colors.Red); // Rot
                    }
                });
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    target.ResponseTime = "Fehler";
                    target.StatusColor = new SolidColorBrush(Colors.Red); // Rot
                });
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
