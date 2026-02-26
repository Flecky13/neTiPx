using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TimersTimer = System.Timers.Timer;
using neTiPx.WinUI.Helpers;
using neTiPx.WinUI.Models;
using neTiPx.WinUI.Services;

namespace neTiPx.WinUI.ViewModels
{
    public sealed class IpConfigViewModel : ObservableObject
    {
        private readonly ConfigStore _configStore = new ConfigStore();
        private readonly NetworkConfigService _networkService = new NetworkConfigService();
        private readonly TimersTimer _pingTimer;
        private readonly SynchronizationContext? _uiContext;

        private string _selectedIpMode = "DHCP";
        private bool _isManual;
        private string _gatewayAddress = string.Empty;
        private string _dnsServer = string.Empty;
        private string _gatewayStatusText = "Unknown";
        private string _gatewayPingText = "Ping: -";
        private GatewayStatusKind _gatewayStatusKind = GatewayStatusKind.Unknown;
        private string? _selectedAdapter;
        private string _profileName = "IP #1";

        public IpConfigViewModel()
        {
            AdapterList = new ObservableCollection<string>();
            IpModeOptions = new ObservableCollection<string> { "DHCP", "Manual" };
            IpAddresses = new ObservableCollection<IpAddressEntry>();

            LoadAdapters();
            LoadProfileFromConfig();

            AddIpCommand = new RelayCommand(AddIpAddress);
            RemoveIpCommand = new RelayCommand<IpAddressEntry>(RemoveIpAddress);
            ApplyCommand = new RelayCommand(ApplyProfile);
            SaveCommand = new RelayCommand(SaveProfile);
            CloseCommand = new RelayCommand(() => App.MainWindow.Close());

            _uiContext = SynchronizationContext.Current;
            _pingTimer = new TimersTimer(TimeSpan.FromSeconds(5))
            {
                AutoReset = true
            };
            _pingTimer.Elapsed += async (_, _) => await UpdateGatewayStatusAsync();
            _pingTimer.Start();
        }

        public ObservableCollection<string> AdapterList { get; }

        public string? SelectedAdapter
        {
            get => _selectedAdapter;
            set => SetProperty(ref _selectedAdapter, value);
        }

        public ObservableCollection<string> IpModeOptions { get; }

        public string SelectedIpMode
        {
            get => _selectedIpMode;
            set
            {
                if (SetProperty(ref _selectedIpMode, value))
                {
                    IsManual = string.Equals(value, "Manual", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public bool IsManual
        {
            get => _isManual;
            set => SetProperty(ref _isManual, value);
        }

        public ObservableCollection<IpAddressEntry> IpAddresses { get; }

        public string GatewayAddress
        {
            get => _gatewayAddress;
            set => SetProperty(ref _gatewayAddress, value);
        }

        public string DnsServer
        {
            get => _dnsServer;
            set => SetProperty(ref _dnsServer, value);
        }

        public string GatewayStatusText
        {
            get => _gatewayStatusText;
            set => SetProperty(ref _gatewayStatusText, value);
        }

        public string GatewayPingText
        {
            get => _gatewayPingText;
            set => SetProperty(ref _gatewayPingText, value);
        }

        public GatewayStatusKind GatewayStatusKind
        {
            get => _gatewayStatusKind;
            set => SetProperty(ref _gatewayStatusKind, value);
        }

        public RelayCommand AddIpCommand { get; }
        public RelayCommand<IpAddressEntry> RemoveIpCommand { get; }
        public RelayCommand ApplyCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand CloseCommand { get; }

        private void LoadAdapters()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => n.GetPhysicalAddress() != null && n.GetPhysicalAddress().GetAddressBytes().Length > 0)
                .Select(n => n.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            AdapterList.Clear();
            foreach (var adapter in adapters)
            {
                AdapterList.Add(adapter);
            }
        }

        private void LoadProfileFromConfig()
        {
            var values = _configStore.ReadAll();
            var name = GetFirstProfileName(values) ?? "IP #1";
            _profileName = name;

            var profile = ReadProfile(values, name);
            SelectedAdapter = NormalizeAdapterName(profile.AdapterName);
            SelectedIpMode = profile.Mode;
            GatewayAddress = profile.Gateway;
            DnsServer = profile.Dns;

            IpAddresses.Clear();
            foreach (var entry in profile.IpAddresses)
            {
                IpAddresses.Add(entry);
            }

            if (IpAddresses.Count == 0)
            {
                IpAddresses.Add(new IpAddressEntry());
            }
        }

        private void AddIpAddress()
        {
            IpAddresses.Add(new IpAddressEntry());
        }

        private void RemoveIpAddress(IpAddressEntry? entry)
        {
            if (entry == null)
            {
                return;
            }

            IpAddresses.Remove(entry);
            if (IpAddresses.Count == 0)
            {
                IpAddresses.Add(new IpAddressEntry());
            }
        }

        private void SaveProfile()
        {
            var values = _configStore.ReadAll();
            var profile = BuildProfileFromFields();

            values["Adapter1"] = SelectedAdapter ?? values.GetValueOrDefault("Adapter1", string.Empty);

            UpdateProfile(values, profile);
            _configStore.WriteAll(values);
        }

        private void ApplyProfile()
        {
            var profile = BuildProfileFromFields();
            var (success, error) = _networkService.ApplyProfile(profile);
            if (!success)
            {
                GatewayStatusText = error ?? "Fehler beim Anwenden.";
                GatewayStatusKind = GatewayStatusKind.Bad;
                return;
            }

            SaveProfile();
        }

        private IpProfile BuildProfileFromFields()
        {
            var profile = new IpProfile
            {
                Name = _profileName,
                AdapterName = SelectedAdapter ?? string.Empty,
                Mode = SelectedIpMode,
                Gateway = GatewayAddress.Trim(),
                Dns = DnsServer.Trim()
            };

            foreach (var entry in IpAddresses)
            {
                if (!string.IsNullOrWhiteSpace(entry.IpAddress) || !string.IsNullOrWhiteSpace(entry.SubnetMask))
                {
                    profile.IpAddresses.Add(new IpAddressEntry
                    {
                        IpAddress = entry.IpAddress.Trim(),
                        SubnetMask = entry.SubnetMask.Trim()
                    });
                }
            }

            return profile;
        }

        private static string? GetFirstProfileName(Dictionary<string, string> values)
        {
            if (values.TryGetValue("IpProfileNames", out var names) && !string.IsNullOrWhiteSpace(names))
            {
                return names.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .FirstOrDefault(n => n.Length > 0);
            }

            if (values.Keys.Any(k => k.StartsWith("IpTab1", StringComparison.OrdinalIgnoreCase)))
            {
                return "IP #1";
            }

            return null;
        }

        private IpProfile ReadProfile(Dictionary<string, string> values, string name)
        {
            var profile = new IpProfile { Name = name };

            if (values.TryGetValue($"{name}.Adapter", out var adapter))
            {
                profile.AdapterName = adapter;
            }
            else if (values.TryGetValue("Adapter1", out var adapter1))
            {
                profile.AdapterName = adapter1;
            }
            else if (values.TryGetValue("IpTab1Adapter", out var legacyAdapter))
            {
                profile.AdapterName = legacyAdapter;
            }

            if (values.TryGetValue($"{name}.Mode", out var mode))
            {
                profile.Mode = NormalizeMode(mode);
            }
            else if (values.TryGetValue("IpTab1Mode", out var legacyMode))
            {
                profile.Mode = NormalizeMode(legacyMode);
            }

            if (values.TryGetValue($"{name}.GW", out var gw))
            {
                profile.Gateway = gw;
            }
            else if (values.TryGetValue("IpTab1GW", out var legacyGw))
            {
                profile.Gateway = legacyGw;
            }

            if (values.TryGetValue($"{name}.DNS", out var dns))
            {
                profile.Dns = dns;
            }
            else if (values.TryGetValue("IpTab1DNS", out var legacyDns))
            {
                profile.Dns = legacyDns;
            }

            var entries = ReadProfileIpEntries(values, name);
            foreach (var entry in entries)
            {
                profile.IpAddresses.Add(entry);
            }

            return profile;
        }

        private static List<IpAddressEntry> ReadProfileIpEntries(Dictionary<string, string> values, string name)
        {
            var entries = new List<IpAddressEntry>();

            for (int i = 1; i <= 10; i++)
            {
                if (!values.TryGetValue($"{name}.IP_{i}", out var ip))
                {
                    break;
                }

                values.TryGetValue($"{name}.Subnet_{i}", out var subnet);
                entries.Add(new IpAddressEntry { IpAddress = ip ?? string.Empty, SubnetMask = subnet ?? string.Empty });
            }

            if (entries.Count == 0)
            {
                if (values.TryGetValue("IpTab1IP", out var legacyIp))
                {
                    values.TryGetValue("IpTab1Subnet", out var legacySubnet);
                    entries.Add(new IpAddressEntry { IpAddress = legacyIp ?? string.Empty, SubnetMask = legacySubnet ?? string.Empty });
                }
            }

            return entries;
        }

        private static string NormalizeMode(string mode)
        {
            if (mode.Equals("Manual", StringComparison.OrdinalIgnoreCase))
            {
                return "Manual";
            }

            return "DHCP";
        }

        private string NormalizeAdapterName(string adapter)
        {
            if (string.IsNullOrWhiteSpace(adapter))
            {
                return adapter;
            }

            if (AdapterList.Contains(adapter))
            {
                return adapter;
            }

            var match = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => string.Equals(n.Name, adapter, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Description, adapter, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(n.Name + " - " + n.Description, adapter, StringComparison.OrdinalIgnoreCase));

            return match?.Name ?? adapter;
        }

        private void UpdateProfile(Dictionary<string, string> values, IpProfile profile)
        {
            var names = new List<string>();
            if (values.TryGetValue("IpProfileNames", out var list) && !string.IsNullOrWhiteSpace(list))
            {
                names = list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0)
                    .ToList();
            }

            if (!names.Contains(profile.Name, StringComparer.OrdinalIgnoreCase))
            {
                names.Insert(0, profile.Name);
            }

            values[$"{profile.Name}.Adapter"] = profile.AdapterName;
            values[$"{profile.Name}.Mode"] = profile.Mode;
            values[$"{profile.Name}.GW"] = profile.Gateway;
            values[$"{profile.Name}.DNS"] = profile.Dns;

            var existingKeys = values.Keys.Where(k => k.StartsWith(profile.Name + ".IP_", StringComparison.OrdinalIgnoreCase)
                || k.StartsWith(profile.Name + ".Subnet_", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var key in existingKeys)
            {
                values.Remove(key);
            }

            int index = 1;
            foreach (var entry in profile.IpAddresses)
            {
                if (string.IsNullOrWhiteSpace(entry.IpAddress))
                {
                    continue;
                }

                values[$"{profile.Name}.IP_{index}"] = entry.IpAddress;
                values[$"{profile.Name}.Subnet_{index}"] = entry.SubnetMask;
                index++;
            }

            values["IpProfileNames"] = string.Join(",", names);
        }

        private async Task UpdateGatewayStatusAsync()
        {
            var address = GatewayAddress?.Trim();
            if (string.IsNullOrWhiteSpace(address))
            {
                PostGatewayStatus("No gateway", "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    var ms = reply.RoundtripTime;
                    var statusText = ms <= 20 ? "Reachable" : "Slow";
                    var statusKind = ms <= 20 ? GatewayStatusKind.Good : GatewayStatusKind.Warning;
                    PostGatewayStatus(statusText, $"Ping: {ms} ms", statusKind);
                }
                else
                {
                    PostGatewayStatus("Unreachable", "Ping: timeout", GatewayStatusKind.Bad);
                }
            }
            catch
            {
                PostGatewayStatus("Error", "Ping: failed", GatewayStatusKind.Bad);
            }
        }

        private void PostGatewayStatus(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                GatewayStatusText = statusText;
                GatewayPingText = pingText;
                GatewayStatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                GatewayStatusText = statusText;
                GatewayPingText = pingText;
                GatewayStatusKind = statusKind;
            }, null);
        }
    }
}
