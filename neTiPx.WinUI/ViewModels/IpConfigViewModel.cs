using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
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

        private IpProfile? _selectedProfile;
        private string _gatewayStatusText = "Unbekannt";
        private string _gatewayPingText = "Ping: -";
        private GatewayStatusKind _gatewayStatusKind = GatewayStatusKind.Unknown;
        private string _dns1StatusText = "Unbekannt";
        private string _dns1PingText = "Ping: -";
        private GatewayStatusKind _dns1StatusKind = GatewayStatusKind.Unknown;
        private string _dns2StatusText = "Unbekannt";
        private string _dns2PingText = "Ping: -";
        private GatewayStatusKind _dns2StatusKind = GatewayStatusKind.Unknown;
        private string _validationMessage = string.Empty;
        private bool _hasValidationErrors = false;

        public IpConfigViewModel()
        {
            AdapterList = new ObservableCollection<string>();
            IpModeOptions = new ObservableCollection<string> { "DHCP", "Manual" };
            IpProfiles = new ObservableCollection<IpProfile>();

            LoadAdapters();
            LoadProfilesFromConfig();

            AddIpCommand = new RelayCommand(AddIpAddress);
            RemoveIpCommand = new RelayCommand<IpAddressEntry>(RemoveIpAddress);
            AddProfileCommand = new RelayCommand(AddProfile);
            DeleteProfileCommand = new RelayCommand<IpProfile>(DeleteProfile);
            ApplyCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
            SaveCommand = new RelayCommand(SaveProfile, CanSaveProfile);
            CloseCommand = new RelayCommand(() => App.MainWindow.Close());

            _uiContext = SynchronizationContext.Current;
            _pingTimer = new TimersTimer(TimeSpan.FromSeconds(5))
            {
                AutoReset = true
            };
            _pingTimer.Elapsed += async (_, _) => await UpdateStatusAsync();
            _pingTimer.Start();
        }

        public ObservableCollection<string> AdapterList { get; }
        public ObservableCollection<string> IpModeOptions { get; }
        public ObservableCollection<IpProfile> IpProfiles { get; }

        public IpProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (_selectedProfile != null)
                {
                    _selectedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
                }

                if (SetProperty(ref _selectedProfile, value))
                {
                    if (_selectedProfile != null)
                    {
                        _selectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
                    }

                    OnPropertyChanged(nameof(IsProfileSelected));
                    OnPropertyChanged(nameof(IsManual));
                    ValidateProfile();
                    UpdateStatusAsync().ConfigureAwait(false);
                }
            }
        }

        private void SelectedProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IpProfile.Mode))
            {
                OnPropertyChanged(nameof(IsManual));
            }

            ValidateProfile();
        }

        public bool IsProfileSelected => SelectedProfile != null;

        public bool IsManual => SelectedProfile != null &&
                                string.Equals(SelectedProfile.Mode, "Manual", StringComparison.OrdinalIgnoreCase);

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (SetProperty(ref _validationMessage, value))
                {
                    ApplyCommand?.RaiseCanExecuteChanged();
                    SaveCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasValidationErrors
        {
            get => _hasValidationErrors;
            set
            {
                if (SetProperty(ref _hasValidationErrors, value))
                {
                    ApplyCommand?.RaiseCanExecuteChanged();
                    SaveCommand?.RaiseCanExecuteChanged();
                }
            }
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

        public string Dns1StatusText
        {
            get => _dns1StatusText;
            set => SetProperty(ref _dns1StatusText, value);
        }

        public string Dns1PingText
        {
            get => _dns1PingText;
            set => SetProperty(ref _dns1PingText, value);
        }

        public GatewayStatusKind Dns1StatusKind
        {
            get => _dns1StatusKind;
            set => SetProperty(ref _dns1StatusKind, value);
        }

        public string Dns2StatusText
        {
            get => _dns2StatusText;
            set => SetProperty(ref _dns2StatusText, value);
        }

        public string Dns2PingText
        {
            get => _dns2PingText;
            set => SetProperty(ref _dns2PingText, value);
        }

        public GatewayStatusKind Dns2StatusKind
        {
            get => _dns2StatusKind;
            set => SetProperty(ref _dns2StatusKind, value);
        }

        public RelayCommand AddIpCommand { get; }
        public RelayCommand<IpAddressEntry> RemoveIpCommand { get; }
        public RelayCommand AddProfileCommand { get; }
        public RelayCommand<IpProfile> DeleteProfileCommand { get; }
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

        private void LoadProfilesFromConfig()
        {
            var values = _configStore.ReadAll();
            var profileNames = GetProfileNames(values);

            IpProfiles.Clear();

            if (profileNames.Count == 0)
            {
                // Create default profile
                var defaultProfile = new IpProfile { Name = "IP #1" };
                defaultProfile.IpAddresses.Add(new IpAddressEntry());
                IpProfiles.Add(defaultProfile);
            }
            else
            {
                foreach (var name in profileNames)
                {
                    var profile = ReadProfile(values, name);
                    if (profile.IpAddresses.Count == 0)
                    {
                        profile.IpAddresses.Add(new IpAddressEntry());
                    }
                    IpProfiles.Add(profile);
                }
            }

            SelectedProfile = IpProfiles.FirstOrDefault();
        }

        private void AddProfile()
        {
            var newNumber = IpProfiles.Count + 1;
            var newName = $"IP #{newNumber}";

            // Ensure unique name
            while (IpProfiles.Any(p => p.Name == newName))
            {
                newNumber++;
                newName = $"IP #{newNumber}";
            }

            var newProfile = new IpProfile
            {
                Name = newName,
                Mode = "DHCP"
            };
            newProfile.IpAddresses.Add(new IpAddressEntry());

            IpProfiles.Add(newProfile);
            SelectedProfile = newProfile;
        }

        private void DeleteProfile(IpProfile? profile)
        {
            if (profile == null || IpProfiles.Count <= 1)
            {
                return;
            }

            var index = IpProfiles.IndexOf(profile);
            IpProfiles.Remove(profile);

            // Select adjacent profile
            if (index >= IpProfiles.Count)
            {
                index = IpProfiles.Count - 1;
            }
            SelectedProfile = IpProfiles[index];

            // Remove from config
            var values = _configStore.ReadAll();
            RemoveProfileFromConfig(values, profile.Name);
            _configStore.WriteAll(values);
        }

        private void AddIpAddress()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            SelectedProfile.IpAddresses.Add(new IpAddressEntry());
        }

        private void RemoveIpAddress(IpAddressEntry? entry)
        {
            if (entry == null || SelectedProfile == null)
            {
                return;
            }

            SelectedProfile.IpAddresses.Remove(entry);
            if (SelectedProfile.IpAddresses.Count == 0)
            {
                SelectedProfile.IpAddresses.Add(new IpAddressEntry());
            }
        }

        private bool CanSaveProfile()
        {
            return SelectedProfile != null && !HasValidationErrors;
        }

        private void SaveProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            ValidateProfile();
            if (HasValidationErrors)
            {
                return;
            }

            var values = _configStore.ReadAll();
            UpdateProfile(values, SelectedProfile);
            _configStore.WriteAll(values);

            ValidationMessage = "Profil gespeichert";
        }

        private bool CanApplyProfile()
        {
            return SelectedProfile != null && !HasValidationErrors;
        }

        private void ApplyProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            ValidateProfile();
            if (HasValidationErrors)
            {
                return;
            }

            var (success, error) = _networkService.ApplyProfile(SelectedProfile);
            if (!success)
            {
                ValidationMessage = error ?? "Fehler beim Anwenden.";
                HasValidationErrors = true;
                GatewayStatusText = "Fehler";
                GatewayStatusKind = GatewayStatusKind.Bad;
                return;
            }

            SaveProfile();
            ValidationMessage = "Profil angewendet";
        }

        private static List<string> GetProfileNames(Dictionary<string, string> values)
        {
            if (values.TryGetValue("IpProfileNames", out var names) && !string.IsNullOrWhiteSpace(names))
            {
                return names.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0)
                    .ToList();
            }

            // Check for legacy profile
            if (values.Keys.Any(k => k.StartsWith("IpTab1", StringComparison.OrdinalIgnoreCase)))
            {
                return new List<string> { "IP #1" };
            }

            return new List<string>();
        }

        private IpProfile ReadProfile(Dictionary<string, string> values, string name)
        {
            var profile = new IpProfile { Name = name };

            if (values.TryGetValue($"{name}.Adapter", out var adapter))
            {
                profile.AdapterName = NormalizeAdapterName(adapter);
            }
            else if (values.TryGetValue("Adapter1", out var adapter1))
            {
                profile.AdapterName = NormalizeAdapterName(adapter1);
            }
            else if (values.TryGetValue("IpTab1Adapter", out var legacyAdapter))
            {
                profile.AdapterName = NormalizeAdapterName(legacyAdapter);
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

            // Try new DNS format first (DNS1, DNS2)
            if (values.TryGetValue($"{name}.DNS1", out var dns1))
            {
                profile.Dns1 = dns1;
            }

            if (values.TryGetValue($"{name}.DNS2", out var dns2))
            {
                profile.Dns2 = dns2;
            }

            // Fall back to old DNS format
            if (string.IsNullOrWhiteSpace(profile.Dns1) && string.IsNullOrWhiteSpace(profile.Dns2))
            {
                if (values.TryGetValue($"{name}.DNS", out var dns))
                {
                    profile.Dns = dns; // This will split into Dns1 and Dns2
                }
                else if (values.TryGetValue("IpTab1DNS", out var legacyDns))
                {
                    profile.Dns = legacyDns;
                }
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
                names.Add(profile.Name);
            }

            values[$"{profile.Name}.Adapter"] = profile.AdapterName;
            values[$"{profile.Name}.Mode"] = profile.Mode;
            values[$"{profile.Name}.GW"] = profile.Gateway;
            values[$"{profile.Name}.DNS"] = profile.Dns;
            values[$"{profile.Name}.DNS1"] = profile.Dns1;
            values[$"{profile.Name}.DNS2"] = profile.Dns2;

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

        private void RemoveProfileFromConfig(Dictionary<string, string> values, string profileName)
        {
            // Remove profile from list
            var names = new List<string>();
            if (values.TryGetValue("IpProfileNames", out var list) && !string.IsNullOrWhiteSpace(list))
            {
                names = list.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n.Trim())
                    .Where(n => n.Length > 0 && !string.Equals(n, profileName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            values["IpProfileNames"] = string.Join(",", names);

            // Remove profile keys
            var keysToRemove = values.Keys
                .Where(k => k.StartsWith(profileName + ".", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in keysToRemove)
            {
                values.Remove(key);
            }
        }

        private void ValidateProfile()
        {
            if (SelectedProfile == null)
            {
                ValidationMessage = string.Empty;
                HasValidationErrors = false;
                return;
            }

            var errors = new List<string>();

            // Validate profile name
            if (string.IsNullOrWhiteSpace(SelectedProfile.Name))
            {
                errors.Add("Profilname erforderlich");
            }

            // Validate adapter
            if (string.IsNullOrWhiteSpace(SelectedProfile.AdapterName))
            {
                errors.Add("Netzwerkkarte erforderlich");
            }

            // Validate manual mode settings
            if (IsManual)
            {
                // Validate Gateway
                if (!string.IsNullOrWhiteSpace(SelectedProfile.Gateway) && !IsValidIpAddress(SelectedProfile.Gateway))
                {
                    errors.Add("Gateway-Adresse ungültig");
                }

                // Validate DNS1
                if (!string.IsNullOrWhiteSpace(SelectedProfile.Dns1) && !IsValidIpAddress(SelectedProfile.Dns1))
                {
                    errors.Add("DNS1-Adresse ungültig");
                }

                // Validate DNS2
                if (!string.IsNullOrWhiteSpace(SelectedProfile.Dns2) && !IsValidIpAddress(SelectedProfile.Dns2))
                {
                    errors.Add("DNS2-Adresse ungültig");
                }

                // Validate IP Addresses
                foreach (var entry in SelectedProfile.IpAddresses)
                {
                    if (!string.IsNullOrWhiteSpace(entry.IpAddress))
                    {
                        if (!IsValidIpAddress(entry.IpAddress))
                        {
                            errors.Add($"IP-Adresse ungültig: {entry.IpAddress}");
                        }

                        if (!string.IsNullOrWhiteSpace(entry.SubnetMask) && !IsValidSubnetMask(entry.SubnetMask))
                        {
                            errors.Add($"Subnetzmaske ungültig: {entry.SubnetMask}");
                        }
                    }
                }
            }

            HasValidationErrors = errors.Count > 0;
            ValidationMessage = HasValidationErrors ? string.Join(", ", errors) : "Validierung erfolgreich";
        }

        private static bool IsValidIpAddress(string ipAddress)
        {
            return IPAddress.TryParse(ipAddress, out _);
        }

        private static bool IsValidSubnetMask(string subnetMask)
        {
            if (!IPAddress.TryParse(subnetMask, out var ip))
            {
                return false;
            }

            // Convert to binary and check if it's a valid subnet mask
            var bytes = ip.GetAddressBytes();
            var bits = new System.Collections.BitArray(bytes);
            bool hasZero = false;

            for (int i = bits.Length - 1; i >= 0; i--)
            {
                if (!bits[i])
                {
                    hasZero = true;
                }
                else if (hasZero)
                {
                    return false; // Found 1 after 0
                }
            }

            return true;
        }

        private async Task UpdateStatusAsync()
        {
            if (SelectedProfile == null || !IsManual)
            {
                PostGatewayStatus("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                PostDns1Status("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                PostDns2Status("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            // Check Gateway
            await CheckHostStatusAsync(
                SelectedProfile.Gateway,
                (status, ping, kind) => PostGatewayStatus(status, ping, kind));

            // Check DNS1
            await CheckHostStatusAsync(
                SelectedProfile.Dns1,
                (status, ping, kind) => PostDns1Status(status, ping, kind));

            // Check DNS2
            await CheckHostStatusAsync(
                SelectedProfile.Dns2,
                (status, ping, kind) => PostDns2Status(status, ping, kind));
        }

        private static async Task CheckHostStatusAsync(string address, Action<string, string, GatewayStatusKind> callback)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                callback("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    var ms = reply.RoundtripTime;
                    var statusText = ms <= 20 ? "Erreichbar" : ms <= 100 ? "Langsam" : "Sehr langsam";
                    var statusKind = ms <= 20 ? GatewayStatusKind.Good :
                                   ms <= 100 ? GatewayStatusKind.Warning : GatewayStatusKind.Bad;
                    callback(statusText, $"Ping: {ms} ms", statusKind);
                }
                else
                {
                    callback("Nicht erreichbar", "Ping: timeout", GatewayStatusKind.Bad);
                }
            }
            catch
            {
                callback("Fehler", "Ping: fehlgeschlagen", GatewayStatusKind.Bad);
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

        private void PostDns1Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns1StatusText = statusText;
                Dns1PingText = pingText;
                Dns1StatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns1StatusText = statusText;
                Dns1PingText = pingText;
                Dns1StatusKind = statusKind;
            }, null);
        }

        private void PostDns2Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns2StatusText = statusText;
                Dns2PingText = pingText;
                Dns2StatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns2StatusText = statusText;
                Dns2PingText = pingText;
                Dns2StatusKind = statusKind;
            }, null);
        }
    }
}
