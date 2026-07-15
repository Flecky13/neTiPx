using neTiPx.UI.Avalonia.Helpers;
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neTiPx.Core.Models;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels
{
    public sealed class IpConfigViewModel : ObservableObject
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private static readonly TimeSpan ConnectionMonitoringInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan UncApplyWaitTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan UncApplyPollInterval = TimeSpan.FromSeconds(1);

        private static string T(string key) => _lm.Lang(key);
        private readonly IpProfileStore _ipProfileStore = new IpProfileStore();
        private readonly UncPathStore _uncPathStore = new UncPathStore();
        private readonly RouteProfileStore _routeProfileStore = new RouteProfileStore();
        private readonly UncPathService _uncPathService = new UncPathService();
        private readonly NetworkConfigService _networkService = new NetworkConfigService();
        private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly SynchronizationContext? _uiContext;
        private readonly SemaphoreSlim _statusUpdateLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _monitoringCts;
        private bool _isLoadingProfile;
        private bool _isMonitoringActive;
        private bool _isApplyingProfile;
        private bool _isPageLoaded;

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
        
        // Cache für die echten Netzwerkwerte vom Adapter
        private string _currentGateway = string.Empty;
        private string _currentDns1 = string.Empty;
        private string _currentDns2 = string.Empty;
        
        private string _validationMessage = string.Empty;
        private bool _hasValidationErrors;
        private string _lastActionMessage = "Bereit";
        private bool _showConnectionStatus = false;
        private StatusMessageType _statusMessageType = StatusMessageType.Info;
        private string _systemDnsInfo = string.Empty;
        private bool _showInputValidationErrors;
        private bool _gatewayHasValidationError;
        private bool _dns1HasValidationError;
        private bool _dns2HasValidationError;
        private string? _selectedProfilePersistedName;
        private string _selectedProfileBaseline = string.Empty;

        public event Action<int>? IpAddressAdded;

        public sealed class UncProfileOption
        {
            public string Value { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        public sealed class RouteProfileOption
        {
            public string Value { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        public sealed class RoutePersistenceOption
        {
            public string Value { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        public IpConfigViewModel()
        {
            AdapterList = new ObservableCollection<string>();
            IpModeOptions = new ObservableCollection<string> { "DHCP", "Manual" };
            IpProfiles = new ObservableCollection<IpProfile>();
            UncProfileOptions = new ObservableCollection<UncProfileOption>();
            RouteProfileOptions = new ObservableCollection<RouteProfileOption>();
            RoutePersistenceOptions = new ObservableCollection<RoutePersistenceOption>();

            LoadAdapters();
            LoadProfilesFromConfig();
            RefreshUncProfileOptions();
            RefreshRouteProfileOptions();
            RefreshRoutePersistenceOptions();

            AddIpCommand = new RelayCommand(AddIpAddress, CanAddIpAddress);
            RemoveIpCommand = new RelayCommand<IpAddressEntry>(RemoveIpAddress, CanRemoveIpAddress);
            AddProfileCommand = new RelayCommand(AddProfile);
            CopyProfileCommand = new RelayCommand(CopyProfile, CanCopyProfile);
            DeleteProfileCommand = new RelayCommand<IpProfile>(DeleteProfile);
            ApplyCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
            SaveCommand = new RelayCommand(SaveProfile, CanSaveProfile);
            RefreshSystemDnsCommand = new RelayCommand(RefreshSystemDns);
            // CloseCommand removed for Avalonia - window closing handled differently

            _uiContext = SynchronizationContext.Current;

            var initialStatus = T("IPCONFIG_STATUS_UNKNOWN");
            var initialPing = "Ping: -";
            _gatewayStatusText = initialStatus;
            _dns1StatusText = initialStatus;
            _dns2StatusText = initialStatus;
            _gatewayPingText = initialPing;
            _dns1PingText = initialPing;
            _dns2PingText = initialPing;
            _systemDnsInfo = T("IPCONFIG_NO_DNS_INFO");

            // System DNS Info initial laden
            RefreshSystemDns();
        }

        public ObservableCollection<string> AdapterList { get; }
        public ObservableCollection<string> IpModeOptions { get; }
        public ObservableCollection<IpProfile> IpProfiles { get; }
        public ObservableCollection<UncProfileOption> UncProfileOptions { get; }
        public ObservableCollection<RouteProfileOption> RouteProfileOptions { get; }
        public ObservableCollection<RoutePersistenceOption> RoutePersistenceOptions { get; }

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
                        _selectedProfilePersistedName = _selectedProfile.Name;
                        _showInputValidationErrors = false;
                        GatewayHasValidationError = false;
                        Dns1HasValidationError = false;
                        Dns2HasValidationError = false;
                        ClearIpAddressValidationFlags(_selectedProfile);
                        UpdateIpAddressRemoveState(_selectedProfile);
                        AttachProfileHandlers(_selectedProfile);
                        // Load profile with loading flag set to prevent PropertyChanged events from marking dirty
                        _isLoadingProfile = true;
                        try
                        {
                            EnsureLinkedUncProfileSelection(_selectedProfile);
                            EnsureLinkedRouteProfileSelection(_selectedProfile);
                            LoadProfileSettingsOnProfileChange(_selectedProfile);
                        }
                        finally
                        {
                            _isLoadingProfile = false;
                        }

                        _selectedProfileBaseline = BuildProfileFingerprint(_selectedProfile);
                        _selectedProfile.IsDirty = false;
                        
                        // Verbindungsstatus aktivieren - Monitoring wird nur gestartet wenn Seite sichtbar ist
                        ShowConnectionStatus = true;
                        if (_isPageLoaded)
                        {
                            StartConnectionMonitoring();
                        }
                    }
                    else
                    {
                        _selectedProfileBaseline = string.Empty;
                        ShowConnectionStatus = false;
                        StopConnectionMonitoring();
                    }

                    OnPropertyChanged(nameof(IsProfileSelected));
                    OnPropertyChanged(nameof(IsManual));
                    OnPropertyChanged(nameof(ConfiguredRoutesText));
                    OnPropertyChanged(nameof(RouteApplicationModeText));
                    OnPropertyChanged(nameof(SelectedUncProfileOption));
                    OnPropertyChanged(nameof(SelectedRouteProfileOption));
                    OnPropertyChanged(nameof(SelectedRoutePersistenceOption));
                    OnPropertyChanged(nameof(IsPersistentRouteMode));
                    OnPropertyChanged(nameof(RouteModeCheckboxText));
                    
                    // Lade die echten Netzwerkwerte vom Adapter für die Anzeige
                    if (_selectedProfile != null && !string.IsNullOrWhiteSpace(_selectedProfile.AdapterName))
                    {
                        var config = _networkInfoService.GetIpv4Config(_selectedProfile.AdapterName);
                        if (config != null)
                        {
                            _currentGateway = config.Gateway ?? string.Empty;
                            _currentDns1 = config.Dns1 ?? string.Empty;
                            _currentDns2 = config.Dns2 ?? string.Empty;
                        }
                        else
                        {
                            _currentGateway = string.Empty;
                            _currentDns1 = string.Empty;
                            _currentDns2 = string.Empty;
                        }
                    }
                    else
                    {
                        _currentGateway = string.Empty;
                        _currentDns1 = string.Empty;
                        _currentDns2 = string.Empty;
                    }
                    
                    OnPropertyChanged(nameof(GatewayAddress));
                    OnPropertyChanged(nameof(Dns1Address));
                    OnPropertyChanged(nameof(Dns2Address));
                    OnPropertyChanged(nameof(HasDns2));
                    RefreshActionButtonsState();
                    CopyProfileCommand?.NotifyCanExecuteChanged();
                    AddIpCommand?.NotifyCanExecuteChanged();
                    RemoveIpCommand?.NotifyCanExecuteChanged();
                    UpdateStatusAsync().ConfigureAwait(false);
                }
            }
        }

        public UncProfileOption? SelectedUncProfileOption
        {
            get
            {
                if (SelectedProfile == null) return null;
                var linkedName = SelectedProfile.LinkedUncProfileName?.Trim() ?? string.Empty;
                return UncProfileOptions.FirstOrDefault(o => o.Value == linkedName);
            }
            set
            {
                if (SelectedProfile != null)
                {
                    SelectedProfile.LinkedUncProfileName = value?.Value ?? string.Empty;
                    OnPropertyChanged(nameof(SelectedUncProfileOption));
                }
            }
        }

        public RouteProfileOption? SelectedRouteProfileOption
        {
            get
            {
                if (SelectedProfile == null) return null;
                var linkedName = SelectedProfile.LinkedRouteProfileName?.Trim() ?? string.Empty;
                return RouteProfileOptions.FirstOrDefault(o => o.Value == linkedName);
            }
            set
            {
                if (SelectedProfile != null)
                {
                    SelectedProfile.LinkedRouteProfileName = value?.Value ?? string.Empty;
                    
                    // RoutesEnabled automatisch setzen basierend auf Routen-Profil-Auswahl
                    SelectedProfile.RoutesEnabled = !string.IsNullOrWhiteSpace(value?.Value);
                    
                    // Lade Routen aus dem ausgewählten Routen-Profil
                    if (!string.IsNullOrWhiteSpace(value?.Value))
                    {
                        var routeProfiles = _routeProfileStore.LoadProfiles();
                        var selectedRouteProfile = routeProfiles.FirstOrDefault(p =>
                            string.Equals(p.Name, value.Value, StringComparison.OrdinalIgnoreCase));

                        if (selectedRouteProfile != null)
                        {
                            LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Lade Routen aus Profil '{value.Value}': {selectedRouteProfile.Routes.Count} Routen");
                            
                            // Kopiere Routen aus dem Routen-Profil in das IP-Profil
                            SelectedProfile.Routes.Clear();
                            foreach (var route in selectedRouteProfile.Routes)
                            {
                                SelectedProfile.Routes.Add(new RouteEntry
                                {
                                    Destination = route.Destination,
                                    SubnetMask = route.SubnetMask,
                                    Gateway = route.Gateway,
                                    Metric = route.Metric
                                });
                            }
                        }
                    }
                    else
                    {
                        // Wenn kein Routen-Profil ausgewählt ist, lösche alle Routen
                        SelectedProfile.Routes.Clear();
                    }
                    
                    OnPropertyChanged(nameof(SelectedRouteProfileOption));
                    OnPropertyChanged(nameof(ConfiguredRoutesText));
                }
            }
        }

        public RoutePersistenceOption? SelectedRoutePersistenceOption
        {
            get
            {
                if (SelectedProfile == null) return null;
                var value = NormalizeRoutePersistenceMode(SelectedProfile.RoutePersistenceMode);
                return RoutePersistenceOptions.FirstOrDefault(o =>
                    string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (SelectedProfile == null || value == null)
                {
                    return;
                }

                SelectedProfile.RoutePersistenceMode = NormalizeRoutePersistenceMode(value.Value);
                OnPropertyChanged(nameof(SelectedRoutePersistenceOption));
                OnPropertyChanged(nameof(RouteApplicationModeText));
                OnPropertyChanged(nameof(IsPersistentRouteMode));
                OnPropertyChanged(nameof(RouteModeCheckboxText));
            }
        }

        public bool IsPersistentRouteMode
        {
            get => !string.Equals(SelectedProfile?.RoutePersistenceMode, "Temporary", StringComparison.OrdinalIgnoreCase);
            set
            {
                if (SelectedProfile == null)
                {
                    return;
                }

                SelectedProfile.RoutePersistenceMode = value ? "Persistent" : "Temporary";
                OnPropertyChanged(nameof(IsPersistentRouteMode));
                OnPropertyChanged(nameof(RouteModeCheckboxText));
                OnPropertyChanged(nameof(SelectedRoutePersistenceOption));
                OnPropertyChanged(nameof(RouteApplicationModeText));
            }
        }

        public string RouteModeCheckboxText =>
            IsPersistentRouteMode
                ? T("IPCONFIG_ROUTE_MODE_PERSISTENT")
                : T("IPCONFIG_ROUTE_MODE_TEMPORARY");

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
            if (e.PropertyName == nameof(IpProfile.IsDirty))
            {
                RefreshActionButtonsState();
                return;
            }

            // Skip DisplayName changes to prevent feedback loops
            if (e.PropertyName == nameof(IpProfile.DisplayName))
            {
                return;
            }

            if (e.PropertyName == nameof(IpProfile.Mode))
            {
                OnPropertyChanged(nameof(IsManual));
                AddIpCommand?.NotifyCanExecuteChanged();
                RemoveIpCommand?.NotifyCanExecuteChanged();
                
                // Wenn auf DHCP gewechselt wird, aktuellen Netzwerkstatus laden
                if (!IsManual && !_isLoadingProfile)
                {
                    ReloadProfileFromNic();
                }
                
                ValidateProfile();
            }
            else if (e.PropertyName == nameof(IpProfile.AdapterName))
            {
                // Don't reload on adapter change
                ValidateProfile();
                
                // Adapter-Wechsel: Verbindungsstatus neu prüfen (aktualisiert auch Adressen)
                if (ShowConnectionStatus)
                {
                    _ = UpdateStatusAsync();
                }
            }
            else if (e.PropertyName == nameof(IpProfile.Gateway))
            {
                ValidateProfile();
                
                // Gateway geändert: Verbindungsstatus neu prüfen (aktualisiert auch Adressen)
                if (ShowConnectionStatus)
                {
                    _ = UpdateStatusAsync();
                }
            }
            else if (e.PropertyName == nameof(IpProfile.Dns1))
            {
                ValidateProfile();
                
                // DNS1 geändert: Verbindungsstatus neu prüfen (aktualisiert auch Adressen)
                if (ShowConnectionStatus)
                {
                    _ = UpdateStatusAsync();
                }
            }
            else if (e.PropertyName == nameof(IpProfile.Dns2))
            {
                ValidateProfile();
                
                // DNS2 geändert: Verbindungsstatus neu prüfen (aktualisiert auch Adressen und HasDns2)
                OnPropertyChanged(nameof(HasDns2));
                OnPropertyChanged(nameof(Dns2Address));
                if (ShowConnectionStatus)
                {
                    _ = UpdateStatusAsync();
                }
            }
            else
            {
                ValidateProfile();
            }

            if (e.PropertyName == nameof(IpProfile.RoutesEnabled))
            {
                OnPropertyChanged(nameof(SelectedProfile));
            }

            if (e.PropertyName == nameof(IpProfile.RoutePersistenceMode))
            {
                OnPropertyChanged(nameof(SelectedRoutePersistenceOption));
                OnPropertyChanged(nameof(RouteApplicationModeText));
                OnPropertyChanged(nameof(IsPersistentRouteMode));
                OnPropertyChanged(nameof(RouteModeCheckboxText));
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

            if (SelectedProfile != null)
            {
                UpdateIpAddressRemoveState(SelectedProfile);
            }

            RemoveIpCommand?.NotifyCanExecuteChanged();
            AddIpCommand?.NotifyCanExecuteChanged();

            ValidateProfile();
            MarkSelectedProfileDirty();
        }

        private void IpAddressEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IpAddressEntry.HasIpAddressError) ||
                e.PropertyName == nameof(IpAddressEntry.HasSubnetMaskError) ||
                e.PropertyName == nameof(IpAddressEntry.CanRemove))
            {
                return;
            }

            ValidateProfile();
            AddIpCommand?.NotifyCanExecuteChanged();
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

            SelectedProfile.IsDirty = !string.Equals(
                BuildProfileFingerprint(SelectedProfile),
                _selectedProfileBaseline,
                StringComparison.Ordinal);
        }

        private static string NormalizeDirtyValue(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private string BuildProfileFingerprint(IpProfile profile)
        {
            var ipParts = profile.IpAddresses
                .Select(entry => $"{NormalizeDirtyValue(entry.IpAddress)}|{NormalizeDirtyValue(entry.SubnetMask)}")
                .Where(part => !string.Equals(part, "|", StringComparison.Ordinal))
                .ToArray();

            var routeParts = profile.Routes
                .Where(route =>
                    !string.IsNullOrWhiteSpace(route.Destination) ||
                    !string.IsNullOrWhiteSpace(route.SubnetMask) ||
                    !string.IsNullOrWhiteSpace(route.Gateway) ||
                    route.Metric > 0)
                .Select(route =>
                    $"{NormalizeDirtyValue(route.Destination)}|{NormalizeDirtyValue(route.SubnetMask)}|{NormalizeDirtyValue(route.Gateway)}|{route.Metric}")
                .ToArray();

            return string.Join("§", new[]
            {
                NormalizeDirtyValue(profile.Name),
                NormalizeMode(profile.Mode),
                NormalizeDirtyValue(NormalizeAdapterName(profile.AdapterName)),
                NormalizeRoutePersistenceMode(profile.RoutePersistenceMode),
                NormalizeDirtyValue(profile.Gateway),
                NormalizeDirtyValue(profile.Dns1),
                NormalizeDirtyValue(profile.Dns2),
                profile.RoutesEnabled ? "1" : "0",
                NormalizeDirtyValue(profile.LinkedUncProfileName).ToLowerInvariant(),
                string.Join(";", ipParts),
                string.Join(";", routeParts)
            });
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

            _ipProfileStore.SaveProfile(SelectedProfile, _selectedProfilePersistedName);
            _selectedProfilePersistedName = SelectedProfile.Name;
            SelectedProfile.IsDirty = false;
            _selectedProfileBaseline = BuildProfileFingerprint(SelectedProfile);
            ValidationMessage = T("IPCONFIG_MSG_PROFILE_SAVED");
            return true;
        }

        public void DiscardCurrentProfileChangesMarker()
        {
            if (SelectedProfile != null)
            {
                SelectedProfile.IsDirty = false;
                _selectedProfileBaseline = BuildProfileFingerprint(SelectedProfile);
            }
        }

        private void LoadProfileSettingsOnProfileChange(IpProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            // Always load profile from XML first
            if (!_ipProfileStore.TryGetProfile(profile.Name, out var storedProfile))
            {
                return;
            }

            // Copy mode and basic settings from XML
            profile.Mode = storedProfile.Mode;
            profile.AdapterName = NormalizeAdapterName(storedProfile.AdapterName);
            profile.RoutePersistenceMode = NormalizeRoutePersistenceMode(storedProfile.RoutePersistenceMode);
            profile.RoutesEnabled = storedProfile.RoutesEnabled;
            profile.LinkedUncProfileName = storedProfile.LinkedUncProfileName;
            profile.LinkedRouteProfileName = storedProfile.LinkedRouteProfileName;
            profile.Routes.Clear();
            foreach (var route in storedProfile.Routes)
            {
                profile.Routes.Add(new RouteEntry
                {
                    Destination = route.Destination,
                    SubnetMask = route.SubnetMask,
                    Gateway = route.Gateway,
                    Metric = route.Metric > 0 ? route.Metric : 1
                });
            }

            // Wenn ein Routen-Profil verlinkt ist und keine Routen geladen wurden,
            // lade die Routen aus dem Routen-Profil
            var linkedRouteProfileName = profile.LinkedRouteProfileName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(linkedRouteProfileName) && profile.Routes.Count == 0)
            {
                var routeProfiles = _routeProfileStore.LoadProfiles();
                var linkedRouteProfile = routeProfiles.FirstOrDefault(p =>
                    string.Equals(p.Name, linkedRouteProfileName, StringComparison.OrdinalIgnoreCase));

                if (linkedRouteProfile != null)
                {
                    LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Lade Routen aus verlinktem Profil '{linkedRouteProfileName}': {linkedRouteProfile.Routes.Count} Routen");
                    
                    foreach (var route in linkedRouteProfile.Routes)
                    {
                        profile.Routes.Add(new RouteEntry
                        {
                            Destination = route.Destination,
                            SubnetMask = route.SubnetMask,
                            Gateway = route.Gateway,
                            Metric = route.Metric
                        });
                    }
                }
            }

            // If mode is DHCP, load remaining settings from NIC
            if (string.Equals(storedProfile.Mode, "DHCP", StringComparison.OrdinalIgnoreCase))
            {
                var nicLoaded = LoadProfileFromNic(profile);
                ValidationMessage = nicLoaded ? T("IPCONFIG_MSG_SETTINGS_READ") : T("IPCONFIG_MSG_NO_SETTINGS");
                HasValidationErrors = false;
                profile.IsDirty = false;
                return;
            }

            // If mode is Manual, load remaining settings from XML
            profile.Gateway = storedProfile.Gateway;
            profile.Dns1 = storedProfile.Dns1;
            profile.Dns2 = storedProfile.Dns2;

            profile.IpAddresses.Clear();
            foreach (var entry in storedProfile.IpAddresses)
            {
                profile.IpAddresses.Add(new IpAddressEntry
                {
                    IpAddress = entry.IpAddress,
                    SubnetMask = entry.SubnetMask
                });
            }

            if (profile.IpAddresses.Count == 0)
            {
                profile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            }

            ValidationMessage = T("IPCONFIG_MSG_CONFIGURATION_READ");
            HasValidationErrors = false;
            profile.IsDirty = false;
        }

        private void ReloadProfileFromNic()
        {
            if (SelectedProfile == null || string.IsNullOrWhiteSpace(SelectedProfile.AdapterName))
            {
                return;
            }

            try
            {
                _isLoadingProfile = true;

                // After apply: Load fresh settings from NIC
                var nicLoaded = LoadProfileFromNic(SelectedProfile);
                ValidationMessage = nicLoaded ? T("IPCONFIG_MSG_SETTINGS_READ") : T("IPCONFIG_MSG_NO_SETTINGS");
                HasValidationErrors = false;
            }
            finally
            {
                _isLoadingProfile = false;
            }
        }

        private static void LoadProfileFromStore(IpProfile sourceProfile, IpProfile targetProfile)
        {
            targetProfile.Mode = sourceProfile.Mode;
            targetProfile.RoutePersistenceMode = NormalizeRoutePersistenceMode(sourceProfile.RoutePersistenceMode);
            targetProfile.Gateway = sourceProfile.Gateway;
            targetProfile.Dns1 = sourceProfile.Dns1;
            targetProfile.Dns2 = sourceProfile.Dns2;
            targetProfile.RoutesEnabled = sourceProfile.RoutesEnabled;
            targetProfile.LinkedUncProfileName = sourceProfile.LinkedUncProfileName;

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

        public string RouteApplicationModeText =>
            string.Equals(SelectedProfile?.RoutePersistenceMode, "Temporary", StringComparison.OrdinalIgnoreCase)
                ? T("IPCONFIG_ROUTE_MODE_TEMPORARY")
                : T("IPCONFIG_ROUTE_MODE_PERSISTENT");

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (SetProperty(ref _validationMessage, value))
                {
                    ApplyCommand?.NotifyCanExecuteChanged();
                    SaveCommand?.NotifyCanExecuteChanged();
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
                    ApplyCommand?.NotifyCanExecuteChanged();
                    SaveCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public bool GatewayHasValidationError
        {
            get => _gatewayHasValidationError;
            set => SetProperty(ref _gatewayHasValidationError, value);
        }

        public bool Dns1HasValidationError
        {
            get => _dns1HasValidationError;
            set => SetProperty(ref _dns1HasValidationError, value);
        }

        public bool Dns2HasValidationError
        {
            get => _dns2HasValidationError;
            set => SetProperty(ref _dns2HasValidationError, value);
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

        public string LastActionMessage
        {
            get => _lastActionMessage;
            set => SetProperty(ref _lastActionMessage, value);
        }

        public bool ShowConnectionStatus
        {
            get => _showConnectionStatus;
            set => SetProperty(ref _showConnectionStatus, value);
        }

        public StatusMessageType StatusMessageType
        {
            get => _statusMessageType;
            set
            {
                if (SetProperty(ref _statusMessageType, value))
                {
                    OnPropertyChanged(nameof(IsStatusInfo));
                    OnPropertyChanged(nameof(IsStatusSuccess));
                    OnPropertyChanged(nameof(IsStatusError));
                }
            }
        }

        public bool IsStatusInfo => _statusMessageType == StatusMessageType.Info;
        public bool IsStatusSuccess => _statusMessageType == StatusMessageType.Success;
        public bool IsStatusError => _statusMessageType == StatusMessageType.Error;

        public string GatewayAddress => string.IsNullOrWhiteSpace(_currentGateway) ? "-" : _currentGateway;
        
        public string Dns1Address => string.IsNullOrWhiteSpace(_currentDns1) ? "-" : _currentDns1;
        
        public string Dns2Address
        {
            get
            {
                // Bevorzuge aktuellen Wert vom Adapter, falls vorhanden
                if (!string.IsNullOrWhiteSpace(_currentDns2))
                    return _currentDns2;
                
                // Ansonsten zeige den Wert aus dem Profil (besonders wichtig im Manual-Modus)
                if (!string.IsNullOrWhiteSpace(SelectedProfile?.Dns2))
                    return SelectedProfile.Dns2;
                
                return "-";
            }
        }
        
        public bool HasDns2 => !string.IsNullOrWhiteSpace(_currentDns2) || 
                                !string.IsNullOrWhiteSpace(SelectedProfile?.Dns2);

        public string SystemDnsInfo
        {
            get => _systemDnsInfo;
            set => SetProperty(ref _systemDnsInfo, value);
        }

        public bool ShowGatewayStatus => _settingsService.GetCheckConnectionGateway();

        public bool ShowDns1Status => _settingsService.GetCheckConnectionDns1();

        public bool ShowDns2Status => _settingsService.GetCheckConnectionDns2();

        public bool ShowConnectionQualityIndicator =>
            _settingsService.GetCheckConnectionGateway() ||
            _settingsService.GetCheckConnectionDns1() ||
            _settingsService.GetCheckConnectionDns2();

        public bool IsSaveHighlighted => SelectedProfile?.IsDirty == true;

        public bool IsApplyHighlighted => SelectedProfile != null && SelectedProfile.IsDirty == false;

        public RelayCommand AddIpCommand { get; }
        public RelayCommand<IpAddressEntry> RemoveIpCommand { get; }
        public RelayCommand AddProfileCommand { get; }
        public RelayCommand CopyProfileCommand { get; }
        public RelayCommand<IpProfile> DeleteProfileCommand { get; }
        public RelayCommand ApplyCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand RefreshSystemDnsCommand { get; }

        private void LoadAdapters()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => !n.IsReceiveOnly)
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
                defaultProfile.RoutePersistenceMode = "Persistent";
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
                AdapterName = null,
                RoutePersistenceMode = "Persistent",
                LinkedUncProfileName = string.Empty
            };
            newProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            newProfile.IsDirty = false;

            IpProfiles.Add(newProfile);
            SelectedProfile = newProfile;
            
            // Automatisch speichern
            _ipProfileStore.SaveProfile(newProfile);
            _selectedProfilePersistedName = newProfile.Name;
            _selectedProfileBaseline = BuildProfileFingerprint(newProfile);
            
            // Tray-Menü aktualisieren
            App.TrayService?.RefreshMenu();
        }

        private bool CanCopyProfile()
        {
            return SelectedProfile != null;
        }

        private void CopyProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            var source = SelectedProfile;
            var copiedProfile = new IpProfile
            {
                Name = BuildUniqueCopyProfileName(source.Name),
                AdapterName = source.AdapterName,
                Mode = source.Mode,
                Gateway = source.Gateway,
                Dns1 = source.Dns1,
                Dns2 = source.Dns2,
                RoutePersistenceMode = NormalizeRoutePersistenceMode(source.RoutePersistenceMode),
                LinkedUncProfileName = source.LinkedUncProfileName,
                RoutesEnabled = source.RoutesEnabled,
                IsDirty = false
            };

            foreach (var entry in source.IpAddresses)
            {
                copiedProfile.IpAddresses.Add(new IpAddressEntry
                {
                    IpAddress = entry.IpAddress,
                    SubnetMask = entry.SubnetMask
                });
            }

            foreach (var route in source.Routes)
            {
                copiedProfile.Routes.Add(new RouteEntry
                {
                    Destination = route.Destination,
                    SubnetMask = route.SubnetMask,
                    Gateway = route.Gateway,
                    Metric = route.Metric
                });
            }

            if (copiedProfile.IpAddresses.Count == 0)
            {
                copiedProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            }

            IpProfiles.Add(copiedProfile);
            SelectedProfile = copiedProfile;
            
            // Automatisch speichern
            _ipProfileStore.SaveProfile(copiedProfile);
            _selectedProfilePersistedName = copiedProfile.Name;
            _selectedProfileBaseline = BuildProfileFingerprint(copiedProfile);
            
            // Tray-Menü aktualisieren
            App.TrayService?.RefreshMenu();
        }

        private string BuildUniqueCopyProfileName(string sourceName)
        {
            var baseName = string.IsNullOrWhiteSpace(sourceName) ? "IP" : sourceName.Trim();
            var copyBase = $"{baseName} Copy";
            var candidate = copyBase;
            var suffix = 2;

            while (IpProfiles.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = $"{copyBase} {suffix}";
                suffix++;
            }

            return candidate;
        }

        private void DeleteProfile(IpProfile? profile)
        {
            if (profile == null || IpProfiles.Count <= 1)
            {
                return;
            }

            LogHandler.LogUserEvent("IpConfig", "ButtonClick", "ProfileDelete", new Dictionary<string, string?>
            {
                ["Profile"] = profile.Name
            });

            LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Profil löschen: '{profile.Name}'");

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
            
            // Tray-Menü aktualisieren
            App.TrayService?.RefreshMenu();
        }

        private void AddIpAddress()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            if (!CanAddIpAddress())
            {
                _showInputValidationErrors = true;
                ValidateProfile(true);
                return;
            }

            SelectedProfile.IpAddresses.Add(new IpAddressEntry { SubnetMask = "255.255.255.0" });
            IpAddressAdded?.Invoke(SelectedProfile.IpAddresses.Count - 1);
            if (_showInputValidationErrors)
            {
                ValidateProfile(true);
            }
        }

        private bool CanAddIpAddress()
        {
            if (SelectedProfile == null || !IsManual)
            {
                return false;
            }

            foreach (var entry in SelectedProfile.IpAddresses)
            {
                if (string.IsNullOrWhiteSpace(entry.IpAddress) || string.IsNullOrWhiteSpace(entry.SubnetMask))
                {
                    return false;
                }

                if (!IsValidIpAddress(entry.IpAddress) || !IsValidSubnetMask(entry.SubnetMask))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanRemoveIpAddress(IpAddressEntry? entry)
        {
            if (!IsManual || entry == null || SelectedProfile == null)
            {
                return false;
            }

            if (SelectedProfile.IpAddresses.Count <= 1)
            {
                return false;
            }

            return !ReferenceEquals(SelectedProfile.IpAddresses[0], entry);
        }

        private void RemoveIpAddress(IpAddressEntry? entry)
        {
            if (entry == null || SelectedProfile == null)
            {
                return;
            }

            if (SelectedProfile.IpAddresses.Count <= 1)
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
            // Speichern ist immer möglich, wenn ein Profil ausgewählt ist
            return SelectedProfile != null;
        }

        private void SaveProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            LogHandler.LogUserEvent("IpConfig", "ButtonClick", "ProfileSave", new Dictionary<string, string?>
            {
                ["Profile"] = SelectedProfile.Name
            });

            _showInputValidationErrors = true;
            ValidateProfile(true);
            
            // Speichern ist immer möglich, aber mit Warnung bei Validierungsfehlern
            if (HasValidationErrors)
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "IpConfig", $"Profil mit Validierungsfehlern gespeichert: '{SelectedProfile.Name}'");
                LastActionMessage = $"Profil '{SelectedProfile.Name}' gespeichert (mit Validierungsfehlern).";
                StatusMessageType = StatusMessageType.Error;
            }
            else
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Profil speichern: '{SelectedProfile.Name}'");
                LastActionMessage = $"Profil '{SelectedProfile.Name}' erfolgreich gespeichert.";
                StatusMessageType = StatusMessageType.Success;
            }

            _ipProfileStore.SaveProfile(SelectedProfile, _selectedProfilePersistedName);
            _selectedProfilePersistedName = SelectedProfile.Name;

            ValidationMessage = T("IPCONFIG_MSG_PROFILE_SAVED");
            SelectedProfile.IsDirty = false;
            _selectedProfileBaseline = BuildProfileFingerprint(SelectedProfile);
            LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Profil gespeichert: '{SelectedProfile.Name}'");
            
            // Tray-Menü aktualisieren (z.B. bei Umbenennung)
            App.TrayService?.RefreshMenu();
            
            // Sofortige Statusprüfung nach Speichern (aktualisiert auch die Adressen-Anzeige)
            if (ShowConnectionStatus)
            {
                _ = UpdateStatusAsync();
            }
        }

        private bool CanApplyProfile()
        {
            return SelectedProfile != null && !SelectedProfile.IsDirty && !HasValidationErrors && !_isApplyingProfile;
        }

        private void RefreshActionButtonsState()
        {
            OnPropertyChanged(nameof(IsSaveHighlighted));
            OnPropertyChanged(nameof(IsApplyHighlighted));
            ApplyCommand?.NotifyCanExecuteChanged();
            SaveCommand?.NotifyCanExecuteChanged();
        }

        private async void ApplyProfile()
        {
            if (SelectedProfile == null)
            {
                return;
            }

            LogHandler.LogUserEvent("IpConfig", "ButtonClick", "ProfileApply", new Dictionary<string, string?>
            {
                ["Profile"] = SelectedProfile.Name,
                ["Adapter"] = SelectedProfile.AdapterName,
                ["Mode"] = SelectedProfile.Mode
            });

            _showInputValidationErrors = true;
            ValidateProfile(true);
            if (HasValidationErrors)
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "IpConfig", $"Profil anwenden abgebrochen (Validierungsfehler): '{SelectedProfile.Name}'");
                LastActionMessage = "Anwenden abgebrochen: Validierungsfehler";
                StatusMessageType = StatusMessageType.Error;
                return;
            }

            LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Profil anwenden: '{SelectedProfile.Name}', Adapter='{SelectedProfile.AdapterName}', Modus='{SelectedProfile.Mode}'");

            _isApplyingProfile = true;
            RefreshActionButtonsState();

            var profile = SelectedProfile;

            // Lade Routen aus dem verlinkten Routen-Profil, falls vorhanden
            var linkedRouteProfileName = profile.LinkedRouteProfileName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(linkedRouteProfileName))
            {
                var routeProfiles = _routeProfileStore.LoadProfiles();
                var linkedRouteProfile = routeProfiles.FirstOrDefault(p =>
                    string.Equals(p.Name, linkedRouteProfileName, StringComparison.OrdinalIgnoreCase));

                if (linkedRouteProfile != null)
                {
                    LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Lade Routen aus Profil '{linkedRouteProfileName}': {linkedRouteProfile.Routes.Count} Routen");
                    
                    // Kopiere Routen aus dem Routen-Profil in das IP-Profil
                    profile.Routes.Clear();
                    foreach (var route in linkedRouteProfile.Routes)
                    {
                        profile.Routes.Add(new RouteEntry
                        {
                            Destination = route.Destination,
                            SubnetMask = route.SubnetMask,
                            Gateway = route.Gateway,
                            Metric = route.Metric
                        });
                    }
                    
                    // Aktiviere Routen, wenn welche geladen wurden
                    profile.RoutesEnabled = profile.Routes.Count > 0;
                    LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"RoutesEnabled={profile.RoutesEnabled}, Route Count={profile.Routes.Count}");
                }
                else
                {
                    LogHandler.LogSystemMessage(LogLevel.WARN, "IpConfig", $"Routen-Profil '{linkedRouteProfileName}' nicht gefunden");
                }
            }

            try
            {
                var (success, error) = await Task.Run(() => _networkService.ApplyProfile(profile));
                if (!success)
                {
                    LogHandler.LogErrorMessage("IpConfig", $"Profil anwenden fehlgeschlagen: {error}");
                    ValidationMessage = error ?? T("IPCONFIG_MSG_APPLY_ERROR");
                    LastActionMessage = $"Fehler beim Anwenden: {error ?? "Unbekannter Fehler"}";
                    StatusMessageType = StatusMessageType.Error;
                    HasValidationErrors = true;
                    GatewayStatusText = T("ADAPTER_STA_Error");
                    GatewayStatusKind = GatewayStatusKind.Bad;
                    ShowConnectionStatus = false;
                    return;
                }

                LogHandler.LogSystemMessage(LogLevel.INFO, "IpConfig", $"Profil erfolgreich angewendet: '{profile.Name}'");
                LastActionMessage = $"Profil '{profile.Name}' erfolgreich angewendet.";
                StatusMessageType = StatusMessageType.Success;
                ShowConnectionStatus = true;
                RefreshSystemDns();

                // After apply: In DHCP mode, reload settings from NIC
                // In Manual mode, keep user's settings and just save them
                if (profile.Mode.Equals("DHCP", StringComparison.OrdinalIgnoreCase))
                {
                    ReloadProfileFromNic();
                }
                
                SaveProfile();
                profile.IsDirty = false;

                var uncProfileName = profile.LinkedUncProfileName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(uncProfileName))
                {
                    ValidationMessage = T("IPCONFIG_MSG_PROFILE_APPLIED");
                    LastActionMessage = $"Profil '{profile.Name}' erfolgreich angewendet.";
                    StatusMessageType = StatusMessageType.Success;
                    HasValidationErrors = false;
                    return;
                }

                ValidationMessage = T("IPCONFIG_MSG_WAITING_NETWORK_BEFORE_UNC");
                HasValidationErrors = false;

                var networkReady = await WaitForAdapterAfterApplyAsync(profile, UncApplyWaitTimeout, CancellationToken.None);
                if (!networkReady)
                {
                    ValidationMessage = T("IPCONFIG_MSG_UNC_WAIT_TIMEOUT");
                    LastActionMessage = "Timeout beim Warten auf Netzwerkverbindung.";
                    StatusMessageType = StatusMessageType.Error;
                    HasValidationErrors = true;
                    return;
                }

                var uncProfiles = _uncPathStore.LoadProfiles();
                var linkedUncProfile = uncProfiles.FirstOrDefault(p =>
                    string.Equals(p.Name, uncProfileName, StringComparison.OrdinalIgnoreCase));

                if (linkedUncProfile == null)
                {
                    ValidationMessage = T("IPCONFIG_MSG_UNC_PROFILE_NOT_FOUND");
                    LastActionMessage = $"UNC-Profil '{uncProfileName}' nicht gefunden.";
                    StatusMessageType = StatusMessageType.Error;
                    HasValidationErrors = true;
                    return;
                }

                var (uncSuccess, uncMessage) = await _uncPathService.ApplyProfile(linkedUncProfile);
                if (!uncSuccess)
                {
                    LogHandler.LogSystemMessage(LogLevel.WARN, "IpConfig", $"UNC-Profil anwenden fehlgeschlagen: {uncMessage}");
                    ValidationMessage = T("IPCONFIG_MSG_UNC_PROFILE_APPLY_FAILED");
                    LastActionMessage = $"UNC-Profil anwenden fehlgeschlagen: {uncMessage}";
                    StatusMessageType = StatusMessageType.Error;
                    HasValidationErrors = true;
                    return;
                }

                ValidationMessage = T("IPCONFIG_MSG_PROFILE_AND_UNC_APPLIED");
                LastActionMessage = $"Profil und UNC-Profil '{uncProfileName}' erfolgreich angewendet.";
                StatusMessageType = StatusMessageType.Success;
                HasValidationErrors = false;
            }
            finally
            {
                _isApplyingProfile = false;
                RefreshActionButtonsState();
            }
        }

        public void RefreshUncProfileOptions()
        {
            var profiles = _uncPathStore.LoadProfiles()
                .Select(p => p.Name?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            UncProfileOptions.Clear();
            UncProfileOptions.Add(new UncProfileOption
            {
                Value = string.Empty,
                DisplayName = T("IPCONFIG_NONE_OPTION")
            });

            foreach (var profileName in profiles)
            {
                UncProfileOptions.Add(new UncProfileOption
                {
                    Value = profileName,
                    DisplayName = profileName
                });
            }

            if (SelectedProfile != null)
            {
                EnsureLinkedUncProfileSelection(SelectedProfile);
            }
        }

        public void RefreshRouteProfileOptions()
        {
            var profiles = _routeProfileStore.LoadProfiles()
                .Select(p => p.Name?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            RouteProfileOptions.Clear();
            RouteProfileOptions.Add(new RouteProfileOption
            {
                Value = string.Empty,
                DisplayName = T("IPCONFIG_NONE_OPTION")
            });

            foreach (var profileName in profiles)
            {
                RouteProfileOptions.Add(new RouteProfileOption
                {
                    Value = profileName,
                    DisplayName = profileName
                });
            }

            if (SelectedProfile != null)
            {
                EnsureLinkedRouteProfileSelection(SelectedProfile);
            }
        }

        public void RefreshRoutePersistenceOptions()
        {
            RoutePersistenceOptions.Clear();
            RoutePersistenceOptions.Add(new RoutePersistenceOption
            {
                Value = "Persistent",
                DisplayName = T("IPCONFIG_ROUTE_MODE_PERSISTENT")
            });
            RoutePersistenceOptions.Add(new RoutePersistenceOption
            {
                Value = "Temporary",
                DisplayName = T("IPCONFIG_ROUTE_MODE_TEMPORARY")
            });

            if (SelectedProfile != null)
            {
                SelectedProfile.RoutePersistenceMode = NormalizeRoutePersistenceMode(SelectedProfile.RoutePersistenceMode);
                OnPropertyChanged(nameof(SelectedRoutePersistenceOption));
                OnPropertyChanged(nameof(RouteApplicationModeText));
            }
        }

        private void EnsureLinkedUncProfileSelection(IpProfile profile)
        {
            var linkedName = profile.LinkedUncProfileName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(linkedName))
            {
                profile.LinkedUncProfileName = string.Empty;
                return;
            }

            if (UncProfileOptions.Any(option =>
                !string.IsNullOrWhiteSpace(option.Value) &&
                string.Equals(option.Value, linkedName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            profile.LinkedUncProfileName = string.Empty;
        }

        private void EnsureLinkedRouteProfileSelection(IpProfile profile)
        {
            var linkedName = profile.LinkedRouteProfileName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(linkedName))
            {
                profile.LinkedRouteProfileName = string.Empty;
                profile.RoutesEnabled = false;
                return;
            }

            if (RouteProfileOptions.Any(option =>
                !string.IsNullOrWhiteSpace(option.Value) &&
                string.Equals(option.Value, linkedName, StringComparison.OrdinalIgnoreCase)))
            {
                profile.RoutesEnabled = true;
                return;
            }

            profile.LinkedRouteProfileName = string.Empty;
            profile.RoutesEnabled = false;
        }

        private static async Task<bool> WaitForAdapterAfterApplyAsync(IpProfile profile, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(profile.AdapterName))
            {
                return false;
            }

            var expectedManualIp = profile.Mode.Equals("Manual", StringComparison.OrdinalIgnoreCase)
                ? profile.IpAddresses.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.IpAddress))?.IpAddress?.Trim()
                : string.Empty;

            var networkInfoService = new NetworkInfoService();
            var startedAt = DateTime.UtcNow;

            while (DateTime.UtcNow - startedAt < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var config = networkInfoService.GetIpv4Config(profile.AdapterName);
                if (config != null && config.IpAddresses.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(expectedManualIp))
                    {
                        return true;
                    }

                    var hasExpectedIp = config.IpAddresses.Any(ip =>
                        string.Equals(ip.IpAddress, expectedManualIp, StringComparison.OrdinalIgnoreCase));

                    if (hasExpectedIp)
                    {
                        return true;
                    }
                }

                await Task.Delay(UncApplyPollInterval, cancellationToken);
            }

            return false;
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
                    // Split DNS string into Dns1 and Dns2
                    var dnsParts = dns?.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    profile.Dns1 = dnsParts.Length > 0 ? dnsParts[0].Trim() : string.Empty;
                    profile.Dns2 = dnsParts.Length > 1 ? dnsParts[1].Trim() : string.Empty;
                }
                else if (values.TryGetValue("IpTab1DNS", out var legacyDns))
                {
                    // Split legacy DNS string into Dns1 and Dns2
                    var dnsParts = legacyDns?.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
                    profile.Dns1 = dnsParts.Length > 0 ? dnsParts[0].Trim() : string.Empty;
                    profile.Dns2 = dnsParts.Length > 1 ? dnsParts[1].Trim() : string.Empty;
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

        private static string NormalizeRoutePersistenceMode(string? mode)
        {
            if (string.Equals(mode, "Temporary", StringComparison.OrdinalIgnoreCase))
            {
                return "Temporary";
            }

            return "Persistent";
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
                Dns1HasValidationError = false;
                Dns2HasValidationError = false;
                return;
            }

            var errors = new List<string>();
            GatewayHasValidationError = false;
            Dns1HasValidationError = false;
            Dns2HasValidationError = false;
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
                    if (markFieldErrors)
                    {
                        Dns1HasValidationError = true;
                    }
                }

                // Validate DNS2
                if (!string.IsNullOrWhiteSpace(SelectedProfile.Dns2) && !IsValidIpAddress(SelectedProfile.Dns2))
                {
                    errors.Add(T("IPCONFIG_ERR_DNS2_INVALID"));
                    if (markFieldErrors)
                    {
                        Dns2HasValidationError = true;
                    }
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

        private static void UpdateIpAddressRemoveState(IpProfile profile)
        {
            for (int i = 0; i < profile.IpAddresses.Count; i++)
            {
                profile.IpAddresses[i].CanRemove = i > 0;
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
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return false;
            }

            var input = ipAddress.Trim();
            var parts = input.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (part.Length == 0)
                {
                    return false;
                }

                if (!part.All(char.IsDigit))
                {
                    return false;
                }

                if (!byte.TryParse(part, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidSubnetMask(string subnetMask)
        {
            return TryNormalizeSubnetMask(subnetMask, out _);
        }

        private static bool IsNetworkAddress(string destination, string subnetMask)
        {
            if (!IPAddress.TryParse(destination, out var destinationIp) ||
                !TryNormalizeSubnetMask(subnetMask, out var normalizedSubnetMask) ||
                !IPAddress.TryParse(normalizedSubnetMask, out var subnetIp))
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
                if (!TryNormalizeSubnetMask(subnet, out var normalizedSubnet))
                {
                    return false;
                }

                if (!IPAddress.TryParse(ip, out var ipAddr) ||
                    !IPAddress.TryParse(testIp, out var testAddr) ||
                    !IPAddress.TryParse(normalizedSubnet, out var subnetAddr))
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

        private static bool TryNormalizeSubnetMask(string input, out string normalizedSubnetMask)
        {
            normalizedSubnetMask = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var value = input.Trim();
            if (value.StartsWith("/", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            if (int.TryParse(value, out var prefixLength))
            {
                if (prefixLength <= 0 || prefixLength > 32)
                {
                    return false;
                }

                normalizedSubnetMask = PrefixLengthToSubnetMask(prefixLength);
                return true;
            }

            if (!IPAddress.TryParse(value, out var ip))
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
            if ((inverted & (inverted + 1)) != 0)
            {
                return false;
            }

            normalizedSubnetMask = value;
            return true;
        }

        private static string PrefixLengthToSubnetMask(int prefixLength)
        {
            var mask = prefixLength == 0 ? 0u : uint.MaxValue << (32 - prefixLength);
            var first = (mask >> 24) & 0xFF;
            var second = (mask >> 16) & 0xFF;
            var third = (mask >> 8) & 0xFF;
            var fourth = mask & 0xFF;
            return $"{first}.{second}.{third}.{fourth}";
        }

        private async Task UpdateStatusAsync()
        {
            await _statusUpdateLock.WaitAsync();
            try
            {
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] UpdateStatusAsync tick (monitoringActive={_isMonitoringActive})");

                if (!_isMonitoringActive)
                {
                    return;
                }

                var selectedProfile = SelectedProfile;
                if (selectedProfile == null)
                {
                    var status = T("ADAPTER_STA_NotConfigured");
                    var ping = "Ping: -";
                    
                    _currentGateway = string.Empty;
                    _currentDns1 = string.Empty;
                    _currentDns2 = string.Empty;
                    
                    PostGatewayStatus(status, ping, GatewayStatusKind.Unknown);
                    PostDns1Status(status, ping, GatewayStatusKind.Unknown);
                    PostDns2Status(status, ping, GatewayStatusKind.Unknown);
                    
                    OnPropertyChanged(nameof(GatewayAddress));
                    OnPropertyChanged(nameof(Dns1Address));
                    OnPropertyChanged(nameof(Dns2Address));
                    OnPropertyChanged(nameof(HasDns2));
                    return;
                }

                // Prüfe ob Adapter konfiguriert ist
                if (string.IsNullOrWhiteSpace(selectedProfile.AdapterName))
                {
                    var status = T("ADAPTER_STA_NotConfigured");
                    var ping = "Ping: -";
                    
                    _currentGateway = string.Empty;
                    _currentDns1 = string.Empty;
                    _currentDns2 = string.Empty;
                    
                    PostGatewayStatus(status, ping, GatewayStatusKind.Unknown);
                    PostDns1Status(status, ping, GatewayStatusKind.Unknown);
                    PostDns2Status(status, ping, GatewayStatusKind.Unknown);
                    
                    OnPropertyChanged(nameof(GatewayAddress));
                    OnPropertyChanged(nameof(Dns1Address));
                    OnPropertyChanged(nameof(Dns2Address));
                    OnPropertyChanged(nameof(HasDns2));
                    return;
                }

                // Lese die echten Netzwerkeinstellungen vom Adapter
                var config = _networkInfoService.GetIpv4Config(selectedProfile.AdapterName);
                if (config == null)
                {
                    var status = T("ADAPTER_STA_NotConfigured");
                    var ping = "Ping: -";
                    
                    _currentGateway = string.Empty;
                    _currentDns1 = string.Empty;
                    _currentDns2 = string.Empty;
                    
                    PostGatewayStatus(status, ping, GatewayStatusKind.Unknown);
                    PostDns1Status(status, ping, GatewayStatusKind.Unknown);
                    PostDns2Status(status, ping, GatewayStatusKind.Unknown);
                    
                    OnPropertyChanged(nameof(GatewayAddress));
                    OnPropertyChanged(nameof(Dns1Address));
                    OnPropertyChanged(nameof(Dns2Address));
                    OnPropertyChanged(nameof(HasDns2));
                    return;
                }

                // Aktualisiere Cache mit den echten Werten
                _currentGateway = config.Gateway ?? string.Empty;
                _currentDns1 = config.Dns1 ?? string.Empty;
                _currentDns2 = config.Dns2 ?? string.Empty;
                
                // Benachrichtige UI über Änderungen
                OnPropertyChanged(nameof(GatewayAddress));
                OnPropertyChanged(nameof(Dns1Address));
                OnPropertyChanged(nameof(Dns2Address));
                OnPropertyChanged(nameof(HasDns2));

                // Verwende die echten Werte von der Netzwerkkarte
                var gateway = NormalizeHostAddress(config.Gateway);
                var dns1 = NormalizeHostAddress(config.Dns1);
                var dns2 = NormalizeHostAddress(config.Dns2);

                // Immer prüfen, wenn ShowConnectionStatus aktiv ist, sonst nur wenn in Settings aktiviert
                bool checkGateway = ShowConnectionStatus || _settingsService.GetCheckConnectionGateway();
                bool checkDns1 = ShowConnectionStatus || _settingsService.GetCheckConnectionDns1();
                bool checkDns2 = ShowConnectionStatus || _settingsService.GetCheckConnectionDns2();

                if (checkGateway)
                {
                    await CheckHostStatusAsync(gateway, (status, ping, kind) => PostGatewayStatus(status, ping, kind));
                }
                else
                {
                    PostGatewayStatus(T("ADAPTER_STA_Disabled"), "Ping: -", GatewayStatusKind.Unknown);
                }

                if (checkDns1)
                {
                    await CheckHostStatusAsync(dns1, (status, ping, kind) => PostDns1Status(status, ping, kind));
                }
                else
                {
                    PostDns1Status(T("ADAPTER_STA_Disabled"), "Ping: -", GatewayStatusKind.Unknown);
                }

                if (checkDns2)
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
            finally
            {
                _statusUpdateLock.Release();
            }
        }

        private async Task RunConnectionMonitoringLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UpdateStatusAsync();

                try
                {
                    await Task.Delay(ConnectionMonitoringInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
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
            if (_isMonitoringActive)
            {
                return;
            }

            _isMonitoringActive = true;
            Debug.WriteLine("[ReachabilityDebug][IpConfigPage] StartConnectionMonitoring");

            _monitoringCts?.Cancel();
            _monitoringCts?.Dispose();

            _monitoringCts = new CancellationTokenSource();
            _ = RunConnectionMonitoringLoopAsync(_monitoringCts.Token);
        }

        private void RefreshSystemDns()
        {
            try
            {
                // Versuche resolvectl status zu verwenden (systemd-resolved)
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "resolvectl",
                        Arguments = "status",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    // Parse die DNS Server aus der Ausgabe
                    var dnsServers = new List<string>();
                    var lines = output.Split('\n');
                    
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (trimmedLine.StartsWith("DNS Servers:", StringComparison.OrdinalIgnoreCase) ||
                            trimmedLine.StartsWith("Current DNS Server:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = trimmedLine.Split(':', 2);
                            if (parts.Length == 2)
                            {
                                var servers = parts[1].Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                                dnsServers.AddRange(servers);
                            }
                        }
                        else if (trimmedLine.Length > 0 && 
                                 char.IsDigit(trimmedLine[0]) && 
                                 (trimmedLine.Contains('.') || trimmedLine.Contains(':')))
                        {
                            // Zeile könnte eine IP-Adresse sein
                            var possibleIp = trimmedLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
                            if (System.Net.IPAddress.TryParse(possibleIp, out _))
                            {
                                if (!dnsServers.Contains(possibleIp))
                                {
                                    dnsServers.Add(possibleIp);
                                }
                            }
                        }
                    }

                    if (dnsServers.Count > 0)
                    {
                        var distinctServers = dnsServers.Distinct().Take(5).ToList();
                        SystemDnsInfo = $"{T("IPCONFIG_ACTIVE_DNS_SERVERS")}:\n{string.Join("\n", distinctServers)}";
                        
                        // Prüfe auf Loopback
                        if (distinctServers.Any(s => s.StartsWith("127.") || s == "::1"))
                        {
                            SystemDnsInfo += $"\n\n{T("IPCONFIG_LOOPBACK_DNS_DETECTED")}";
                        }
                    }
                    else
                    {
                        SystemDnsInfo = T("IPCONFIG_NO_DNS_RESOLVECTL");
                    }
                }
                else
                {
                    // Fallback: /etc/resolv.conf lesen
                    TryReadResolvConf();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IpConfig] Fehler beim Abrufen der DNS-Informationen: {ex.Message}");
                // Fallback: /etc/resolv.conf lesen
                TryReadResolvConf();
            }
        }

        private void TryReadResolvConf()
        {
            try
            {
                var resolvConfPath = "/etc/resolv.conf";
                if (File.Exists(resolvConfPath))
                {
                    var lines = File.ReadAllLines(resolvConfPath);
                    var nameservers = lines
                        .Where(l => l.Trim().StartsWith("nameserver", StringComparison.OrdinalIgnoreCase))
                        .Select(l => l.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                        .Where(parts => parts.Length >= 2)
                        .Select(parts => parts[1])
                        .ToList();

                    if (nameservers.Count > 0)
                    {
                        SystemDnsInfo = $"{T("IPCONFIG_DNS_RESOLVCONF_TITLE")}:\n{string.Join("\n", nameservers)}";
                        
                        if (nameservers.Any(s => s.StartsWith("127.")))
                        {
                            SystemDnsInfo += $"\n\n{T("IPCONFIG_LOOPBACK_DNS_DETECTED")}";
                        }
                    }
                    else
                    {
                        SystemDnsInfo = T("IPCONFIG_NO_DNS_RESOLVCONF");
                    }
                }
                else
                {
                    SystemDnsInfo = T("IPCONFIG_NO_DNS_INFO");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[IpConfig] Fehler beim Lesen von resolv.conf: {ex.Message}");
                SystemDnsInfo = $"{T("IPCONFIG_DNS_ERROR_PREFIX")}: {ex.Message}";
            }
        }

        public void StopConnectionMonitoring()
        {
            if (!_isMonitoringActive)
            {
                return;
            }

            _isMonitoringActive = false;
            Debug.WriteLine("[ReachabilityDebug][IpConfigPage] StopConnectionMonitoring");

            _monitoringCts?.Cancel();
            _monitoringCts?.Dispose();
            _monitoringCts = null;
        }

        public void OnPageLoaded()
        {
            _isPageLoaded = true;
            Debug.WriteLine("[ReachabilityDebug][IpConfigPage] OnPageLoaded");
            
            // Starte Monitoring nur wenn ein Profil ausgewählt ist
            if (SelectedProfile != null && ShowConnectionStatus)
            {
                StartConnectionMonitoring();
            }
        }

        public void OnPageUnloaded()
        {
            _isPageLoaded = false;
            Debug.WriteLine("[ReachabilityDebug][IpConfigPage] OnPageUnloaded");
            
            // Stoppe Monitoring wenn Seite nicht mehr sichtbar
            StopConnectionMonitoring();
        }
    }

    public enum StatusMessageType
    {
        Info,
        Success,
        Error
    }
}
