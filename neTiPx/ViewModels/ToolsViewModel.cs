using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using neTiPx.Helpers;
using neTiPx.Services;

namespace neTiPx.ViewModels
{
    public sealed class ToolsViewModel : ObservableObject
    {
        private readonly PingTargetsStore _pingTargetsStore = new PingTargetsStore();
        private string _pingInfo = "Ping-Eintraege aus PingTargets.xml";
        private string _wifiInfo = "Wifi-Scan via Native WiFi API";
        private DispatcherQueue _dispatcherQueue;

        public ToolsViewModel()
        {
            PingEntries = new ObservableCollection<string>();
            WifiNetworks = new ObservableCollection<WifiNetwork>();

            RefreshPingCommand = new RelayCommand(LoadPingEntries);
            ScanWifiCommand = new RelayCommand(ScanWifiNetworksAsync);

            // Capture the current dispatcher queue
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            LoadPingEntries();
        }

        public ObservableCollection<string> PingEntries { get; }

        public ObservableCollection<WifiNetwork> WifiNetworks { get; }

        public string PingInfo
        {
            get => _pingInfo;
            set => SetProperty(ref _pingInfo, value);
        }

        public string WifiInfo
        {
            get => _wifiInfo;
            set => SetProperty(ref _wifiInfo, value);
        }

        public RelayCommand RefreshPingCommand { get; }

        public RelayCommand ScanWifiCommand { get; }

        private void LoadPingEntries()
        {
            PingEntries.Clear();
            var targets = _pingTargetsStore.ReadAll();
            foreach (var target in targets)
            {
                if (!string.IsNullOrWhiteSpace(target.Target))
                {
                    PingEntries.Add(target.Target);
                }
            }

            if (PingEntries.Count == 0)
            {
                PingEntries.Add("Keine Ping-Eintraege gefunden.");
            }
        }

        private void ScanWifiNetworksAsync()
        {
            // Show loading state
            WifiInfo = "Scanning...";

            Task.Run(() =>
            {
                try
                {
                    Debug.WriteLine("[ToolsViewModel] WiFi scan starting...");
                    var networks = WifiScanner.ScanWifiNetworks();

                    Debug.WriteLine($"[ToolsViewModel] Scan returned {networks?.Count ?? 0} networks");

                    // Update UI on dispatcher queue
                    if (_dispatcherQueue != null)
                    {
                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            try
                            {
                                WifiNetworks.Clear();

                                if (networks == null || networks.Count == 0)
                                {
                                    WifiInfo = "Keine Netzwerke gefunden.";
                                    Debug.WriteLine("[ToolsViewModel] No networks found or scan returned null");
                                    return;
                                }

                                foreach (var network in networks)
                                {
                                    WifiNetworks.Add(network);
                                    Debug.WriteLine($"[ToolsViewModel] Added: {network.SSID} ({network.SignalStrengthPercent}%)");
                                }

                                WifiInfo = $"Scan complete: {WifiNetworks.Count} networks found";
                                Debug.WriteLine($"[ToolsViewModel] WiFi scan complete: {WifiNetworks.Count} networks");
                            }
                            catch (Exception ex)
                            {
                                WifiInfo = $"UI Update Error: {ex.Message}";
                                Debug.WriteLine($"[ToolsViewModel] Error updating UI: {ex}");
                            }
                        });
                    }
                    else
                    {
                        WifiInfo = "Dispatcher not available";
                        Debug.WriteLine("[ToolsViewModel] Dispatcher queue is null");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ToolsViewModel] WiFi scan error: {ex}");

                    if (_dispatcherQueue != null)
                    {
                        _dispatcherQueue.TryEnqueue(() =>
                        {
                            WifiInfo = $"Scan error: {ex.Message}";
                        });
                    }
                }
            });
        }
    }
}
