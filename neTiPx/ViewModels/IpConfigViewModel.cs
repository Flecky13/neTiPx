using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TimersTimer = System.Timers.Timer;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;

namespace neTiPx.ViewModels
{
    public sealed class IpConfigViewModel : ObservableObject
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;

        private static string T(string key) => _lm.Lang(key);

        private readonly ConfigStore _configStore = new ConfigStore();
        private readonly IpProfileStore _ipProfileStore = new IpProfileStore();
        private readonly NetworkConfigService _networkService = new NetworkConfigService();
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly TimersTimer _pingTimer;
        private readonly SynchronizationContext? _uiContext;
        private bool _isLoadingProfile = false;
        private bool _isMonitoringActive;

        private IpProfile? _selectedProfile;
        private string _gatewayStatusText = string.Empty;
        private string _gatewayPingText = string.Empty;
        private GatewayStatusKind _gatewayStatusKind = GatewayStatusKind.Unknown;
        private string _dns1StatusText = string.Empty;
        private string _dns1PingText = string.Empty;
        private GatewayStatusKind _dns1StatusKind = GatewayStatusKind.Unknown;
        private string _dns2StatusText = string.Empty;
        private string _dns2PingText = string.Empty;
        private GatewayStatusKind _dns2StatusKind = GatewayStatusKind.Unknown;
        private string _validationMessage = string.Empty;
        private bool _hasValidationErrors = false;
        private bool _showInputValidationErrors;
        private bool _gatewayHasValidationError;

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

            var initialStatus = T("IPCONFIG_STATUS_UNKNOWN");
            var initialPing = "Ping: -";
            _gatewayStatusText = initialStatus;
            _dns1StatusText = initialStatus;
            _dns2StatusText = initialStatus;
            _gatewayPingText = initialPing;
            _dns1PingText = initialPing;
            _dns2PingText = initialPing;
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
                    DetachProfileHandlers(_selectedProfile);
                }

                if (SetProperty(ref _selectedProfile, value))
                {
                    if (_selectedProfile != null)
                    {
                        _showInputValidationErrors = false;
                        GatewayHasValidationError = false;
                        ClearIpAddressValidationFlags(_selectedProfile);
                        AttachProfileHandlers(_selectedProfile);
                        LoadProfileSettingsOnProfileChangeAsync().ConfigureAwait(false);
                    }

                    OnPropertyChanged(nameof(IsProfileSelected));
                    OnPropertyChanged(nameof(IsManual));
                    OnPropertyChanged(nameof(ConfiguredRoutesText));
                    OnPropertyChanged(nameof(RouteApplicationModeText));
                    UpdateStatusAsync().ConfigureAwait(false);
                }
            }
        }

        private void AttachProfileHandlers(IpProfile profile)
        {
            profile.PropertyChanged += SelectedProfile_PropertyChanged;
            profile.IpAddresses.CollectionChanged += SelectedProfile_IpAddresses_CollectionChanged;
            profile.Routes.CollectionChanged += SelectedProfile_Routes_CollectionChanged;

            foreach (var entry in profile.IpAddresses)
            {
                entry.PropertyChanged += IpAddressEntry_PropertyChanged;
            }

            foreach (var route in profile.Routes)
            {
                route.PropertyChanged += RouteEntry_PropertyChanged;
            }
        }

        private void DetachProfileHandlers(IpProfile profile)
        {
            profile.PropertyChanged -= SelectedProfile_PropertyChanged;
            profile.IpAddresses.CollectionChanged -= SelectedProfile_IpAddresses_CollectionChanged;
            profile.Routes.CollectionChanged -= SelectedProfile_Routes_CollectionChanged;

            foreach (var entry in profile.IpAddresses)
            {
                entry.PropertyChanged -= IpAddressEntry_PropertyChanged;
            }

            foreach (var route in profile.Routes)
            {
                route.PropertyChanged -= RouteEntry_PropertyChanged;
            }
        }

        private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Skip IsDirty and DisplayName changes to prevent feedback loops
            if (e.PropertyName == nameof(IpProfile.IsDirty) || e.PropertyName == nameof(IpProfile.DisplayName))
            {
                return;
            }

            if (e.PropertyName == nameof(IpProfile.Mode))
            {
                OnPropertyChanged(nameof(IsManual));
                ValidateProfile();
            }
            else if (e.PropertyName == nameof(IpProfile.AdapterName))
            {
                // Don't reload on adapter change
                ValidateProfile();
            }
            else
            {
                ValidateProfile();
            }

            if (e.PropertyName == nameof(IpProfile.AddRoutesOnApply))
            {
                OnPropertyChanged(nameof(RouteApplicationModeText));
                OnPropertyChanged(nameof(SelectedProfile));
            }

            if (e.PropertyName == nameof(IpProfile.RoutesEnabled))
            {
                OnPropertyChanged(nameof(SelectedProfile));
            }

            MarkSelectedProfileDirty();
        }

        private void SelectedProfile_IpAddresses_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<IpAddressEntry>())
                {
                    item.PropertyChanged -= IpAddressEntry_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<IpAddressEntry>())
                {
                    item.PropertyChanged += IpAddressEntry_PropertyChanged;
                }
            }

            ValidateProfile();
            MarkSelectedProfileDirty();
        }

        private void IpAddressEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IpAddressEntry.HasIpAddressError) ||
                e.PropertyName == nameof(IpAddressEntry.HasSubnetMaskError))
            {
                return;
            }

            ValidateProfile();
            MarkSelectedProfileDirty();
        }

        private void SelectedProfile_Routes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<RouteEntry>())
                {
                    item.PropertyChanged -= RouteEntry_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<RouteEntry>())
                {
                    item.PropertyChanged += RouteEntry_PropertyChanged;
                }
            }

            ValidateProfile();
            OnPropertyChanged(nameof(ConfiguredRoutesText));
            MarkSelectedProfileDirty();
        }

        private void RouteEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ValidateProfile();
            MarkSelectedProfileDirty();
        }

        private void MarkSelectedProfileDirty()
        {
            if (_isLoadingProfile || SelectedProfile == null)
            {
                return;
            }

            SelectedProfile.IsDirty = true;
        }

        public bool SaveCurrentProfileForProfileSwitch()
        {
            if (SelectedProfile == null)
            {
                return true;
            }

            ValidateProfile();
            if (HasValidationErrors)
            {
                return false;
            }

            _ipProfileStore.SaveProfile(SelectedProfile);
            SelectedProfile.IsDirty = false;
            ValidationMessage = T("IPCONFIG_MSG_PROFILE_SAVED");
            return true;
        }

        public void DiscardCurrentProfileChangesMarker()
        {
            if (SelectedProfile != null)
            {
                SelectedProfile.IsDirty = false;
            }
        }

        private Task LoadProfileSettingsOnProfileChangeAsync()
        {
            if (SelectedProfile == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                _isLoadingProfile = true;

                // Always load profile from XML first
                if (!_ipProfileStore.TryGetProfile(SelectedProfile.Name, out var storedProfile))
                {
                    return Task.CompletedTask;
                }

                // Copy mode and basic settings from XML
                SelectedProfile.Mode = storedProfile.Mode;
                SelectedProfile.AdapterName = NormalizeAdapterName(storedProfile.AdapterName);
                SelectedProfile.RoutesEnabled = storedProfile.RoutesEnabled;
                SelectedProfile.AddRoutesOnApply = storedProfile.AddRoutesOnApply;
                SelectedProfile.Routes.Clear();
                foreach (var route in storedProfile.Routes)
                {
                    SelectedProfile.Routes.Add(new RouteEntry
                    {
                        Destination = route.Destination,
                        SubnetMask = route.SubnetMask,
                        Gateway = route.Gateway,
                        Metric = route.Metric > 0 ? route.Metric : 1
                    });
                }

                // If mode is DHCP, load remaining settings from NIC
                if (string.Equals(storedProfile.Mode, "DHCP", StringComparison.OrdinalIgnoreCase))
                {
                    var nicLoaded = LoadProfileFromNic(SelectedProfile);
                    ValidationMessage = nicLoaded ? T("IPCONFIG_MSG_SETTINGS_READ") : T("IPCONFIG_MSG_NO_SETTINGS");
                    HasValidationErrors = false;
                    SelectedProfile.IsDirty = false;
                    return Task.CompletedTask;
                }

                // If mode is Manual, load remaining settings from XML
                SelectedProfile.Gateway = storedProfile.Gateway;
                SelectedProfile.Dns1 = storedProfile.Dns1;
                SelectedProfile.Dns2 = storedProfile.Dns2;

                SelectedProfile.IpAddresses.Clear();
                foreach (var entry in storedProfile.IpAddresses)
                {
                    SelectedProfile.IpAddresses.Add(new IpAddressEntry
                    {
                        IpAddress = entry.IpAddress,
                        SubnetMask = entry.SubnetMask
                    });
                }

                if (SelectedProfile.IpAddresses.Count == 0)
                {
                    SelectedProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
                }

                ValidationMessage = T("IPCONFIG_MSG_CONFIGURATION_READ");
                HasValidationErrors = false;
                SelectedProfile.IsDirty = false;
                return Task.CompletedTask;
            }
            finally
            {
                _isLoadingProfile = false;
            }
        }

        private Task ReloadProfileFromNicAsync()
        {
            if (SelectedProfile == null || string.IsNullOrWhiteSpace(SelectedProfile.AdapterName))
            {
                return Task.CompletedTask;
            }

            try
            {
                _isLoadingProfile = true;

                // After apply: Load fresh settings from NIC
                var nicLoaded = LoadProfileFromNic(SelectedProfile);
                ValidationMessage = nicLoaded ? T("IPCONFIG_MSG_SETTINGS_READ") : T("IPCONFIG_MSG_NO_SETTINGS");
                HasValidationErrors = false;
                return Task.CompletedTask;
            }
            finally
            {
                _isLoadingProfile = false;
            }
        }

        private Task ReloadSelectedProfileSettingsAsync()
        {
            if (SelectedProfile == null)
            {
                return Task.CompletedTask;
            }

            try
            {
                _isLoadingProfile = true;

                if (string.Equals(SelectedProfile.Mode, "DHCP", StringComparison.OrdinalIgnoreCase))
                {
                    var loaded = LoadProfileFromNic(SelectedProfile);
                    ValidationMessage = loaded ? T("IPCONFIG_MSG_SETTINGS_READ") : T("IPCONFIG_MSG_NO_SETTINGS");
                    HasValidationErrors = false;
                    return Task.CompletedTask;
                }

                var hasConfigSettings = _ipProfileStore.HasPersistedProfileSettings(SelectedProfile.Name);

                if (hasConfigSettings && _ipProfileStore.TryGetProfile(SelectedProfile.Name, out var storedProfile))
                {
                    LoadProfileFromStore(storedProfile, SelectedProfile);
                    ValidationMessage = T("IPCONFIG_MSG_CONFIGURATION_READ");
                    HasValidationErrors = false;
                    return Task.CompletedTask;
                }

                var nicLoaded = LoadProfileFromNic(SelectedProfile);
                ValidationMessage = nicLoaded ? T("IPCONFIG_MSG_SETTINGS_READ") : T("IPCONFIG_MSG_NO_SETTINGS");
                HasValidationErrors = false;
                return Task.CompletedTask;
            }
            finally
            {
                _isLoadingProfile = false;
            }
        }

        private static void LoadProfileFromStore(IpProfile sourceProfile, IpProfile targetProfile)
        {
            targetProfile.Mode = sourceProfile.Mode;
            targetProfile.Gateway = sourceProfile.Gateway;
            targetProfile.Dns1 = sourceProfile.Dns1;
            targetProfile.Dns2 = sourceProfile.Dns2;
            targetProfile.RoutesEnabled = sourceProfile.RoutesEnabled;

            targetProfile.IpAddresses.Clear();
            foreach (var entry in sourceProfile.IpAddresses)
            {
                targetProfile.IpAddresses.Add(new IpAddressEntry
                {
                    IpAddress = entry.IpAddress,
                    SubnetMask = entry.SubnetMask
                });
            }

            if (targetProfile.IpAddresses.Count == 0)
            {
                targetProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            }

            targetProfile.Routes.Clear();
            foreach (var route in sourceProfile.Routes)
            {
                targetProfile.Routes.Add(new RouteEntry
                {
                    Destination = route.Destination,
                    SubnetMask = route.SubnetMask,
                    Gateway = route.Gateway,
                    Metric = route.Metric > 0 ? route.Metric : 1
                });
            }
        }

        private static bool LoadProfileFromNic(IpProfile targetProfile)
        {
            if (string.IsNullOrWhiteSpace(targetProfile.AdapterName))
            {
                return false;
            }

            var networkInfoService = new NetworkInfoService();
            var config = networkInfoService.GetIpv4Config(targetProfile.AdapterName);
            if (config == null)
            {
                return false;
            }

            targetProfile.Gateway = config.Gateway ?? string.Empty;
            targetProfile.Dns1 = config.Dns1 ?? string.Empty;
            targetProfile.Dns2 = config.Dns2 ?? string.Empty;

            targetProfile.IpAddresses.Clear();
            foreach (var (ipAddress, subnetMask) in config.IpAddresses)
            {
                targetProfile.IpAddresses.Add(new IpAddressEntry
                {
                    IpAddress = ipAddress,
                    SubnetMask = subnetMask
                });
            }

            if (targetProfile.IpAddresses.Count == 0)
            {
                targetProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            }

            return true;
        }

        private static bool HasPersistedProfileSettings(Dictionary<string, string> values, string profileName)
        {
            static bool HasValue(Dictionary<string, string> source, string key)
            {
                return source.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value);
            }

            if (HasValue(values, $"{profileName}.GW") ||
                HasValue(values, $"{profileName}.DNS1") ||
                HasValue(values, $"{profileName}.DNS2") ||
                HasValue(values, $"{profileName}.DNS"))
            {
                return true;
            }

            for (int i = 1; i <= 10; i++)
            {
                if (HasValue(values, $"{profileName}.IP_{i}"))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsProfileSelected => SelectedProfile != null;

        public bool IsManual => SelectedProfile != null &&
                                string.Equals(SelectedProfile.Mode, "Manual", StringComparison.OrdinalIgnoreCase);

        public string ConfiguredRoutesText => $"{SelectedProfile?.Routes.Count ?? 0}{T("IPCONFIG_ROUTES_COUNT_SUFFIX")}";

        public string RouteApplicationModeText => SelectedProfile?.AddRoutesOnApply == true ? T("IPCONFIG_ROUTE_ADD") : T("IPCONFIG_ROUTE_SET");

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

        public bool GatewayHasValidationError
        {
            get => _gatewayHasValidationError;
            set => SetProperty(ref _gatewayHasValidationError, value);
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

        public bool ShowGatewayStatus => _settingsService.GetCheckConnectionGateway();

        public bool ShowDns1Status => _settingsService.GetCheckConnectionDns1();

        public bool ShowDns2Status => _settingsService.GetCheckConnectionDns2();

        public bool ShowConnectionQualityIndicator =>
            _settingsService.GetCheckConnectionGateway() ||
            _settingsService.GetCheckConnectionDns1() ||
            _settingsService.GetCheckConnectionDns2();

        public void RefreshConnectionStatusVisibility()
        {
            OnPropertyChanged(nameof(ShowGatewayStatus));
            OnPropertyChanged(nameof(ShowDns1Status));
            OnPropertyChanged(nameof(ShowDns2Status));
            OnPropertyChanged(nameof(ShowConnectionQualityIndicator));
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
            var profiles = _ipProfileStore.ReadAllProfiles();

            IpProfiles.Clear();

            if (profiles.Count == 0)
            {
                // Create default profile
                var defaultProfile = new IpProfile { Name = "IP #1" };
                defaultProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
                defaultProfile.IsDirty = false;
                IpProfiles.Add(defaultProfile);
            }
            else
            {
                foreach (var profile in profiles)
                {
                    profile.AdapterName = NormalizeAdapterName(profile.AdapterName);
                    if (profile.IpAddresses.Count == 0)
                    {
                        profile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
                    }
                    profile.IsDirty = false;
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
                Mode = "DHCP",
                AdapterName = null
            };
            newProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            newProfile.IsDirty = false;

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

            // Remove from profile storage
            _ipProfileStore.RemoveProfile(profile.Name);
        }

        private void AddIpAddress()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            SelectedProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            if (_showInputValidationErrors)
            {
                ValidateProfile(true);
            }
        }

        private void RemoveIpAddress(IpAddressEntry? entry)
        {
            if (entry == null || SelectedProfile == null)
            {
                return;
            }

            if (SelectedProfile.IpAddresses.Contains(entry))
            {
                SelectedProfile.IpAddresses.Remove(entry);
                if (_showInputValidationErrors)
                {
                    ValidateProfile(true);
                }
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

            _showInputValidationErrors = true;
            ValidateProfile(true);
            if (HasValidationErrors)
            {
                return;
            }

            _ipProfileStore.SaveProfile(SelectedProfile);

            ValidationMessage = T("IPCONFIG_MSG_PROFILE_SAVED");
            SelectedProfile.IsDirty = false;
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

            _showInputValidationErrors = true;
            ValidateProfile(true);
            if (HasValidationErrors)
            {
                return;
            }

            var (success, error) = _networkService.ApplyProfile(SelectedProfile);
            if (!success)
            {
                ValidationMessage = error ?? T("IPCONFIG_MSG_APPLY_ERROR");
                HasValidationErrors = true;
                GatewayStatusText = T("ADAPTER_STA_Error");
                GatewayStatusKind = GatewayStatusKind.Bad;
                return;
            }

            // After apply: load fresh settings from NIC and save to INI
            ReloadProfileFromNicAsync().ConfigureAwait(false);
            SaveProfile();
            ValidationMessage = T("IPCONFIG_MSG_PROFILE_APPLIED");
            SelectedProfile.IsDirty = false;
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

        private string? NormalizeAdapterName(string? adapter)
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

            values[$"{profile.Name}.Adapter"] = profile.AdapterName ?? string.Empty;
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
            ValidateProfile(_showInputValidationErrors);
        }

        private void ValidateProfile(bool markFieldErrors)
        {
            if (SelectedProfile == null)
            {
                ValidationMessage = string.Empty;
                HasValidationErrors = false;
                GatewayHasValidationError = false;
                return;
            }

            var errors = new List<string>();
            GatewayHasValidationError = false;
            ClearIpAddressValidationFlags(SelectedProfile);

            // Validate profile name
            if (string.IsNullOrWhiteSpace(SelectedProfile.Name))
            {
                errors.Add(T("IPCONFIG_ERR_PROFILE_NAME_REQUIRED"));
            }

            // Validate adapter
            if (string.IsNullOrWhiteSpace(SelectedProfile.AdapterName))
            {
                errors.Add(T("IPCONFIG_ERR_ADAPTER_REQUIRED"));
            }

            // Validate manual mode settings
            if (IsManual)
            {
                // Validate Gateway
                if (!string.IsNullOrWhiteSpace(SelectedProfile.Gateway) && !IsValidIpAddress(SelectedProfile.Gateway))
                {
                    errors.Add(T("IPCONFIG_ERR_GATEWAY_INVALID"));
                    if (markFieldErrors)
                    {
                        GatewayHasValidationError = true;
                    }
                }

                // Validate DNS1
                if (!string.IsNullOrWhiteSpace(SelectedProfile.Dns1) && !IsValidIpAddress(SelectedProfile.Dns1))
                {
                    errors.Add(T("IPCONFIG_ERR_DNS1_INVALID"));
                }

                // Validate DNS2
                if (!string.IsNullOrWhiteSpace(SelectedProfile.Dns2) && !IsValidIpAddress(SelectedProfile.Dns2))
                {
                    errors.Add(T("IPCONFIG_ERR_DNS2_INVALID"));
                }

                // Validate IP Addresses: first line also checks gateway relation; additional lines only syntax.
                for (int i = 0; i < SelectedProfile.IpAddresses.Count; i++)
                {
                    var entry = SelectedProfile.IpAddresses[i];
                    var hasIp = !string.IsNullOrWhiteSpace(entry.IpAddress);
                    var hasSubnet = !string.IsNullOrWhiteSpace(entry.SubnetMask);

                    if (!hasIp && !hasSubnet)
                    {
                        continue;
                    }

                    var ipValid = hasIp && IsValidIpAddress(entry.IpAddress);
                    var subnetValid = hasSubnet && IsValidSubnetMask(entry.SubnetMask);

                    if (!ipValid)
                    {
                        errors.Add($"{T("IPCONFIG_ERR_IP_INVALID_PREFIX")}{entry.IpAddress}");
                        if (markFieldErrors)
                        {
                            entry.HasIpAddressError = true;
                        }
                    }

                    if (!subnetValid)
                    {
                        errors.Add($"{T("IPCONFIG_ERR_SUBNET_INVALID_PREFIX")}{entry.SubnetMask}");
                        if (markFieldErrors)
                        {
                            entry.HasSubnetMaskError = true;
                        }
                    }

                    if (i == 0 && ipValid && subnetValid && !string.IsNullOrWhiteSpace(SelectedProfile.Gateway) && IsValidIpAddress(SelectedProfile.Gateway))
                    {
                        if (!IsIpInSubnet(entry.IpAddress, SelectedProfile.Gateway, entry.SubnetMask))
                        {
                            errors.Add(T("IPCONFIG_ERR_GATEWAY_INVALID"));
                            if (markFieldErrors)
                            {
                                GatewayHasValidationError = true;
                            }
                        }
                    }
                }
            }

            if (SelectedProfile.RoutesEnabled)
            {
                for (int i = 0; i < SelectedProfile.Routes.Count; i++)
                {
                    var route = SelectedProfile.Routes[i];

                    if (string.IsNullOrWhiteSpace(route.Destination) &&
                        string.IsNullOrWhiteSpace(route.SubnetMask) &&
                        string.IsNullOrWhiteSpace(route.Gateway))
                    {
                        continue;
                    }

                    if (!IsValidIpAddress(route.Destination))
                    {
                        errors.Add($"{T("IPCONFIG_ROUTE_PREFIX")}{i + 1}{T("IPCONFIG_ERR_ROUTE_DEST_INVALID_SUFFIX")}");
                    }

                    if (!IsValidSubnetMask(route.SubnetMask))
                    {
                        errors.Add($"{T("IPCONFIG_ROUTE_PREFIX")}{i + 1}{T("IPCONFIG_ERR_ROUTE_SUBNET_INVALID_SUFFIX")}");
                    }

                    if (!IsValidIpAddress(route.Gateway))
                    {
                        errors.Add($"{T("IPCONFIG_ROUTE_PREFIX")}{i + 1}{T("IPCONFIG_ERR_ROUTE_GATEWAY_INVALID_SUFFIX")}");
                    }

                    if (route.Metric <= 0)
                    {
                        errors.Add($"{T("IPCONFIG_ROUTE_PREFIX")}{i + 1}{T("IPCONFIG_ERR_ROUTE_METRIC_INVALID_SUFFIX")}");
                    }

                    if (IsValidIpAddress(route.Destination) && IsValidSubnetMask(route.SubnetMask) &&
                        !IsNetworkAddress(route.Destination, route.SubnetMask))
                    {
                        errors.Add($"{T("IPCONFIG_ROUTE_PREFIX")}{i + 1}{T("IPCONFIG_ERR_ROUTE_DEST_NETWORK_SUFFIX")}");
                    }
                }
            }

            HasValidationErrors = errors.Count > 0;
            ValidationMessage = HasValidationErrors ? string.Join(", ", errors) : T("IPCONFIG_MSG_VALIDATION_OK");
        }

        private static void ClearIpAddressValidationFlags(IpProfile profile)
        {
            foreach (var entry in profile.IpAddresses)
            {
                entry.HasIpAddressError = false;
                entry.HasSubnetMaskError = false;
            }
        }

        public void RevalidateProfile()
        {
            ValidateProfile();
            OnPropertyChanged(nameof(ConfiguredRoutesText));
            OnPropertyChanged(nameof(RouteApplicationModeText));
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

            var bytes = ip.GetAddressBytes();
            if (bytes.Length != 4)
            {
                return false;
            }

            var mask = ((uint)bytes[0] << 24) |
                       ((uint)bytes[1] << 16) |
                       ((uint)bytes[2] << 8) |
                       bytes[3];

            if (mask == 0)
            {
                return false;
            }

            var inverted = ~mask;
            return (inverted & (inverted + 1)) == 0;
        }

        private static bool IsNetworkAddress(string destination, string subnetMask)
        {
            if (!IPAddress.TryParse(destination, out var destinationIp) || !IPAddress.TryParse(subnetMask, out var subnetIp))
            {
                return false;
            }

            var destinationBytes = destinationIp.GetAddressBytes();
            var subnetBytes = subnetIp.GetAddressBytes();

            if (destinationBytes.Length != 4 || subnetBytes.Length != 4)
            {
                return false;
            }

            var destinationValue = ((uint)destinationBytes[0] << 24) |
                                   ((uint)destinationBytes[1] << 16) |
                                   ((uint)destinationBytes[2] << 8) |
                                   destinationBytes[3];

            var mask = ((uint)subnetBytes[0] << 24) |
                       ((uint)subnetBytes[1] << 16) |
                       ((uint)subnetBytes[2] << 8) |
                       subnetBytes[3];

            return (destinationValue & mask) == destinationValue;
        }

        private static bool IsIpInSubnet(string ip, string testIp, string subnet)
        {
            try
            {
                if (!IPAddress.TryParse(ip, out var ipAddr) ||
                    !IPAddress.TryParse(testIp, out var testAddr) ||
                    !IPAddress.TryParse(subnet, out var subnetAddr))
                {
                    return false;
                }

                var ipBytes = ipAddr.GetAddressBytes();
                var testBytes = testAddr.GetAddressBytes();
                var subnetBytes = subnetAddr.GetAddressBytes();

                if (ipBytes.Length != 4 || testBytes.Length != 4 || subnetBytes.Length != 4)
                {
                    return false;
                }

                uint ipUint = ((uint)ipBytes[0] << 24) | ((uint)ipBytes[1] << 16) | ((uint)ipBytes[2] << 8) | ipBytes[3];
                uint testUint = ((uint)testBytes[0] << 24) | ((uint)testBytes[1] << 16) | ((uint)testBytes[2] << 8) | testBytes[3];
                uint subnetUint = ((uint)subnetBytes[0] << 24) | ((uint)subnetBytes[1] << 16) | ((uint)subnetBytes[2] << 8) | subnetBytes[3];

                uint ipNetwork = ipUint & subnetUint;
                uint testNetwork = testUint & subnetUint;

                return ipNetwork == testNetwork;
            }
            catch
            {
                return false;
            }
        }

        private async Task UpdateStatusAsync()
        {
            try
            {
                var timerEnabled = _pingTimer?.Enabled == true;
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] UpdateStatusAsync tick (timerEnabled={timerEnabled}, monitoringActive={_isMonitoringActive})");

                if (!_isMonitoringActive)
                {
                    return;
                }

                var selectedProfile = SelectedProfile;
                if (selectedProfile == null)
                {
                    var status = T("ADAPTER_STA_NotConfigured");
                    var ping = "Ping: -";
                    PostGatewayStatus(status, ping, GatewayStatusKind.Unknown);
                    PostDns1Status(status, ping, GatewayStatusKind.Unknown);
                    PostDns2Status(status, ping, GatewayStatusKind.Unknown);
                    return;
                }

                var gateway = NormalizeHostAddress(selectedProfile.Gateway);
                var dns1 = NormalizeHostAddress(selectedProfile.Dns1);
                var dns2 = NormalizeHostAddress(selectedProfile.Dns2);

                // Nur prüfen, wenn in den Settings aktiviert
                if (_settingsService.GetCheckConnectionGateway())
                {
                    await CheckHostStatusAsync(gateway, (status, ping, kind) => PostGatewayStatus(status, ping, kind));
                }
                else
                {
                    PostGatewayStatus(T("ADAPTER_STA_Disabled"), "Ping: -", GatewayStatusKind.Unknown);
                }

                if (_settingsService.GetCheckConnectionDns1())
                {
                    await CheckHostStatusAsync(dns1, (status, ping, kind) => PostDns1Status(status, ping, kind));
                }
                else
                {
                    PostDns1Status(T("ADAPTER_STA_Disabled"), "Ping: -", GatewayStatusKind.Unknown);
                }

                if (_settingsService.GetCheckConnectionDns2())
                {
                    await CheckHostStatusAsync(dns2, (status, ping, kind) => PostDns2Status(status, ping, kind));
                }
                else
                {
                    PostDns2Status(T("ADAPTER_STA_Disabled"), "Ping: -", GatewayStatusKind.Unknown);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] UpdateStatusAsync error: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string NormalizeHostAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return string.Empty;
            }

            var candidate = address.Trim();

            if (candidate.Contains(','))
            {
                candidate = candidate.Split(',')[0].Trim();
            }
            else if (candidate.Contains(';'))
            {
                candidate = candidate.Split(';')[0].Trim();
            }
            else if (candidate.Contains(' '))
            {
                candidate = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            }

            return candidate;
        }

        private async Task CheckHostStatusAsync(string address, Action<string, string, GatewayStatusKind> callback)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                Debug.WriteLine("[ReachabilityDebug][IpConfigPage] Skip ping: address empty");
                callback(T("ADAPTER_STA_NotConfigured"), "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            // Nur valide IP-Adressen pingen, um Ausnahme-Spam bei ungültigen Host-Strings zu vermeiden.
            if (!IPAddress.TryParse(address, out var parsedAddress))
            {
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] Skip ping: invalid ip '{address}'");
                callback(T("ADAPTER_STA_NotReachable"), T("ADAPTER_STA_PingInvalidAddress"), GatewayStatusKind.Bad);
                return;
            }

            try
            {
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] Ping -> {parsedAddress}");
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(parsedAddress, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    var ms = reply.RoundtripTime;

                    // Hole die Schwellwerte aus den Einstellungen
                    int thresholdFast = _settingsService.GetPingThresholdFast();
                    int thresholdNormal = _settingsService.GetPingThresholdNormal();

                    var statusText = ms <= thresholdFast ? T("ADAPTER_STA_Reachable") : ms <= thresholdNormal ? T("ADAPTER_STA_Slow") : T("ADAPTER_STA_VerySlow");
                    var statusKind = ms <= thresholdFast ? GatewayStatusKind.Good :
                                   ms <= thresholdNormal ? GatewayStatusKind.Warning : GatewayStatusKind.Bad;
                    callback(statusText, $"Ping: {ms} ms", statusKind);
                }
                else
                {
                    callback(T("ADAPTER_STA_NotReachable"), T("ADAPTER_STA_PingTimeout"), GatewayStatusKind.Bad);
                }
            }
            catch (PingException)
            {
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] PingException -> {address}");
                callback(T("ADAPTER_STA_NotReachable"), T("ADAPTER_STA_PingFailed"), GatewayStatusKind.Bad);
            }
            catch
            {
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] Ping error -> {address}");
                callback(T("ADAPTER_STA_Error"), T("ADAPTER_STA_PingError"), GatewayStatusKind.Bad);
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

        public void StartConnectionMonitoring()
        {
            _isMonitoringActive = true;

            if (!_pingTimer.Enabled)
            {
                Debug.WriteLine("[ReachabilityDebug][IpConfigPage] StartConnectionMonitoring");
                _pingTimer.Start();
                UpdateStatusAsync().ConfigureAwait(false);
            }
        }

        public void StopConnectionMonitoring()
        {
            _isMonitoringActive = false;

            if (_pingTimer.Enabled)
            {
                Debug.WriteLine("[ReachabilityDebug][IpConfigPage] StopConnectionMonitoring");
                _pingTimer.Stop();
            }
        }
    }
}
