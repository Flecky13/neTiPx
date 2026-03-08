using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using neTiPx.Helpers;
using neTiPx.Services;

namespace neTiPx.ViewModels
{
    public sealed class ToolsViewModel : ObservableObject
    {
        private readonly PingTargetsStore _pingTargetsStore = new PingTargetsStore();
        private string _pingInfo = "Ping-Eintraege aus PingTargets.xml";
        private string _wifiInfo = "Wifi-Scan via netsh.";

        public ToolsViewModel()
        {
            PingEntries = new ObservableCollection<string>();
            WifiNetworks = new ObservableCollection<string>();

            RefreshPingCommand = new RelayCommand(LoadPingEntries);
            ScanWifiCommand = new RelayCommand(ScanWifiNetworks);

            LoadPingEntries();
        }

        public ObservableCollection<string> PingEntries { get; }

        public ObservableCollection<string> WifiNetworks { get; }

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

        private void ScanWifiNetworks()
        {
            WifiNetworks.Clear();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show networks mode=bssid",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    WifiNetworks.Add("Scan fehlgeschlagen.");
                    return;
                }

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.TrimStart().StartsWith("SSID", StringComparison.OrdinalIgnoreCase) && line.Contains(":"))
                    {
                        var name = line.Split(new[] { ':' }, 2)[1].Trim();
                        if (!string.IsNullOrWhiteSpace(name) && !WifiNetworks.Contains(name))
                        {
                            WifiNetworks.Add(name);
                        }
                    }
                }

                if (WifiNetworks.Count == 0)
                {
                    WifiNetworks.Add("Keine Netzwerke gefunden.");
                }
            }
            catch
            {
                WifiNetworks.Add("Scan fehlgeschlagen.");
            }
        }
    }
}
