using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.Core.Helpers;
using neTiPx.Core.Models;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private static readonly LanguageManager _lm = LanguageManager.Instance;
    private static string T(string key) => _lm.Lang(key);

    private readonly Core.Services.AdapterStore _adapterStore;
    private readonly Core.Services.HoverWindowSettings _hoverWindowSettings;
    private readonly Core.Services.ThemeService _themeService;
    private readonly SettingsService _settingsService;
    private readonly AutostartService _autostartService;
    private bool _isLoadingDesktopOverlaySettings;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableAdapters;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableSecondaryAdapters;
    
    [ObservableProperty]
    private string? _selectedPrimaryAdapter;
    
    [ObservableProperty]
    private string? _selectedSecondaryAdapter;

    // Theme Einstellungen
    [ObservableProperty]
    private ObservableCollection<string> _availableThemes;

    [ObservableProperty]
    private string? _selectedTheme;

    [ObservableProperty]
    private ObservableCollection<string> _availableLanguages;

    [ObservableProperty]
    private string? _selectedLanguage;

    private readonly Dictionary<string, string> _languageDisplayToCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _themeDisplayToName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _themeNameToDisplay = new(StringComparer.OrdinalIgnoreCase);
    private bool _isRefreshingLocalizedSelections;

    // Info-Fenster Einstellungen
    [ObservableProperty]
    private int _hoverWindowPositionIndex; // 0 = Oben, 1 = Unten

    [ObservableProperty]
    private decimal _hoverWindowRightOffset;

    [ObservableProperty]
    private decimal _hoverWindowVerticalOffset;

    // Verbindungsqualität Einstellungen
    [ObservableProperty]
    private int _pingThresholdFast;

    [ObservableProperty]
    private int _pingThresholdNormal;

    // Autostart Einstellungen
    [ObservableProperty]
    private bool _isAutostartEnabled;
    
    [ObservableProperty]
    private bool _startMinimizedToTray;

    [ObservableProperty]
    private bool _desktopOverlayEnabled;

    [ObservableProperty]
    private bool _desktopOverlayHoverInteractive;

    [ObservableProperty]
    private int _desktopOverlayPositionIndex;

    [ObservableProperty]
    private decimal _desktopOverlayOffsetX;

    [ObservableProperty]
    private decimal _desktopOverlayOffsetY;

    [ObservableProperty]
    private decimal _desktopOverlayWidth;

    [ObservableProperty]
    private decimal _desktopOverlayHeight;

    [ObservableProperty]
    private decimal _desktopOverlayFontSize;

    [ObservableProperty]
    private string _desktopOverlayFontFamily = "Consolas";

    [ObservableProperty]
    private string _desktopOverlayTextColor = "#FFFFFFFF";

    [ObservableProperty]
    private string _desktopOverlayBackgroundColor = "#1A1A1A";

    [ObservableProperty]
    private int _desktopOverlayBackgroundOpacityPercent;

    [ObservableProperty]
    private int _desktopOverlayPadding;

    [ObservableProperty]
    private int _desktopOverlayLineSpacing;

    [ObservableProperty]
    private int _desktopOverlayCornerRadius;

    [ObservableProperty]
    private bool _desktopOverlayShadowEnabled;

    [ObservableProperty]
    private int _desktopOverlayNetworkRefreshSeconds;

    [ObservableProperty]
    private int _desktopOverlayExternalIpRefreshMinutes;

    [ObservableProperty]
    private int _desktopOverlayRamRefreshSeconds;

    [ObservableProperty]
    private int _desktopOverlayUptimeRefreshSeconds;

    [ObservableProperty]
    private ObservableCollection<DesktopOverlayItemSettingViewModel> _desktopOverlayInfoItems;
    
    public SettingsViewModel()
    {
        _adapterStore = new Core.Services.AdapterStore();
        _hoverWindowSettings = new Core.Services.HoverWindowSettings();
        _themeService = new Core.Services.ThemeService();
        _settingsService = new SettingsService();
        _autostartService = new AutostartService();
        _availableAdapters = new ObservableCollection<string>();
        _availableSecondaryAdapters = new ObservableCollection<string>();
        _availableThemes = new ObservableCollection<string>();
        _availableLanguages = new ObservableCollection<string>();
        _desktopOverlayInfoItems = new ObservableCollection<DesktopOverlayItemSettingViewModel>();
        
        LoadAvailableAdapters();
        LoadAdapterSettings();
        LoadHoverWindowSettings();
        LoadThemeSettings();
        LoadLanguageSettings();
        LoadConnectionQualitySettings();
        LoadAutostartSettings();
        LoadDesktopOverlaySettings();
    }
    
    /// <summary>
    /// Lädt alle verfügbaren Netzwerkadapter.
    /// </summary>
    private void LoadAvailableAdapters()
    {
        try
        {
            var noAdapterLabel = GetNoAdapterLabel();
            // Nur sinnvolle Adapter anbieten (verbundene zuerst, keine virtuellen Plattform-Interfaces)
            var adapters = new Core.Services.AdapterDiscoveryService()
                .GetSelectableAdapters()
                .Select(n => n.Name)
                .ToList();
            
            AvailableAdapters.Clear();
            foreach (var adapter in adapters)
            {
                AvailableAdapters.Add(adapter);
            }
            
            // Für den sekundären Adapter: Leeren Eintrag am Anfang hinzufügen
            AvailableSecondaryAdapters.Clear();
            AvailableSecondaryAdapters.Add(noAdapterLabel);
            foreach (var adapter in adapters)
            {
                AvailableSecondaryAdapters.Add(adapter);
            }
        }
        catch
        {
            // Ignore errors during adapter enumeration
        }
    }
    
    /// <summary>
    /// Lädt die Adapter-Einstellungen aus der Adapter.xml.
    /// Falls keine Konfiguration vorhanden, werden die ersten zwei Adapter ausgewählt.
    /// </summary>
    private void LoadAdapterSettings()
    {
        try
        {
            var settings = _adapterStore.ReadAdapters();
            
            // Prüfe ob gespeicherte Adapter noch verfügbar sind
            if (!string.IsNullOrWhiteSpace(settings.PrimaryAdapter) && 
                AvailableAdapters.Contains(settings.PrimaryAdapter))
            {
                SelectedPrimaryAdapter = settings.PrimaryAdapter;
            }
            
            if (!string.IsNullOrWhiteSpace(settings.SecondaryAdapter) && 
                AvailableAdapters.Contains(settings.SecondaryAdapter))
            {
                SelectedSecondaryAdapter = settings.SecondaryAdapter;
            }
            else
            {
                // Wenn kein sekundärer Adapter konfiguriert, zeige "(Kein Adapter)"
                SelectedSecondaryAdapter = GetNoAdapterLabel();
            }
            
            // Fallback: Ersten zwei Adapter auswählen wenn keine gültige Config
            if (string.IsNullOrWhiteSpace(SelectedPrimaryAdapter) && 
                AvailableAdapters.Count > 0)
            {
                SelectedPrimaryAdapter = AvailableAdapters[0];
                
                if (AvailableAdapters.Count > 1)
                {
                    SelectedSecondaryAdapter = AvailableAdapters[1];
                }
                
                // Sofort speichern
                SaveAdapterSettings();
            }
        }
        catch
        {
            // Bei Fehler: Erste zwei Adapter auswählen
            if (AvailableAdapters.Count > 0)
            {
                SelectedPrimaryAdapter = AvailableAdapters[0];
                
                if (AvailableAdapters.Count > 1)
                {
                    SelectedSecondaryAdapter = AvailableAdapters[1];
                }
                else
                {
                    SelectedSecondaryAdapter = GetNoAdapterLabel();
                }
            }
        }
    }
    
    /// <summary>
    /// Wird aufgerufen, wenn sich der primäre Adapter ändert.
    /// Speichert automatisch die Einstellungen.
    /// </summary>
    partial void OnSelectedPrimaryAdapterChanged(string? value)
    {
        if (_isRefreshingLocalizedSelections)
        {
            return;
        }

        SaveAdapterSettings();
    }
    
    /// <summary>
    /// Wird aufgerufen, wenn sich der sekundäre Adapter ändert.
    /// Speichert automatisch die Einstellungen.
    /// </summary>
    partial void OnSelectedSecondaryAdapterChanged(string? value)
    {
        if (_isRefreshingLocalizedSelections)
        {
            return;
        }

        SaveAdapterSettings();
    }
    
    /// <summary>
    /// Speichert die aktuellen Adapter-Einstellungen in die Adapter.xml.
    /// </summary>
    private void SaveAdapterSettings()
    {
        try
        {
            // "(Kein Adapter)" als null/leer speichern
            var secondaryAdapter = SelectedSecondaryAdapter;
            if (IsNoAdapterSelection(secondaryAdapter))
            {
                secondaryAdapter = null;
            }
            
            var settings = new Core.Services.AdapterStore.AdapterSettings
            {
                PrimaryAdapter = SelectedPrimaryAdapter,
                SecondaryAdapter = secondaryAdapter
            };
            
            _adapterStore.WriteAdapters(settings);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Lädt die Info-Fenster-Einstellungen.
    /// </summary>
    private void LoadHoverWindowSettings()
    {
        try
        {
            var settings = _hoverWindowSettings.ReadSettings();
            
            HoverWindowPositionIndex = settings.VerticalAnchor == "Top" ? 0 : 1;
            HoverWindowRightOffset = settings.RightOffsetPixels;
            HoverWindowVerticalOffset = settings.VerticalOffsetPixels;
        }
        catch
        {
            // Defaults
            HoverWindowPositionIndex = 1; // Unten
            HoverWindowRightOffset = 20;
            HoverWindowVerticalOffset = 50;
        }
    }

    /// <summary>
    /// Speichert die Info-Fenster-Einstellungen.
    /// </summary>
    private void SaveHoverWindowSettings()
    {
        try
        {
            var settings = new Core.Services.HoverWindowSettings.Settings
            {
                VerticalAnchor = HoverWindowPositionIndex == 0 ? "Top" : "Bottom",
                RightOffsetPixels = (int)HoverWindowRightOffset,
                VerticalOffsetPixels = (int)HoverWindowVerticalOffset
            };
            
            _hoverWindowSettings.WriteSettings(settings);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich die Position ändert.
    /// </summary>
    partial void OnHoverWindowPositionIndexChanged(int value)
    {
        SaveHoverWindowSettings();
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich der rechte Abstand ändert.
    /// </summary>
    partial void OnHoverWindowRightOffsetChanged(decimal value)
    {
        SaveHoverWindowSettings();
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich der vertikale Abstand ändert.
    /// </summary>
    partial void OnHoverWindowVerticalOffsetChanged(decimal value)
    {
        SaveHoverWindowSettings();
    }

    /// <summary>
    /// Lädt die verfügbaren Themes und das ausgewählte Theme.
    /// </summary>
    private void LoadThemeSettings()
    {
        try
        {
            var themes = _themeService.GetAllThemes();
            AvailableThemes.Clear();
            _themeDisplayToName.Clear();
            _themeNameToDisplay.Clear();

            foreach (var theme in themes)
            {
                var displayName = T(GetThemeLocalizationKey(theme.Name));
                AvailableThemes.Add(displayName);
                _themeDisplayToName[displayName] = theme.Name;
                _themeNameToDisplay[theme.Name] = displayName;
            }

            var savedThemeName = _themeService.ReadThemeName();
            if (_themeNameToDisplay.TryGetValue(savedThemeName, out var savedDisplayName))
            {
                SelectedTheme = savedDisplayName;
            }
            else if (_themeNameToDisplay.TryGetValue("Blau", out var defaultDisplayName))
            {
                SelectedTheme = defaultDisplayName;
            }
            else
            {
                SelectedTheme = AvailableThemes.FirstOrDefault();
            }
        }
        catch
        {
            // Defaults
            AvailableThemes.Add(T("SETTINGS_THEME_BLUE"));
            SelectedTheme = T("SETTINGS_THEME_BLUE");
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich das ausgewählte Theme ändert.
    /// </summary>
    partial void OnSelectedThemeChanged(string? value)
    {
        if (_isRefreshingLocalizedSelections)
        {
            return;
        }

        if (value != null)
        {
            var themeName = _themeDisplayToName.TryGetValue(value, out var mappedName) ? mappedName : value;
            SaveThemeSettings(themeName);
            ApplyTheme(themeName);
        }
    }

    private static string GetThemeLocalizationKey(string themeName)
    {
        return themeName switch
        {
            "Rot" => "SETTINGS_THEME_RED",
            "Orange" => "SETTINGS_THEME_ORANGE",
            "Prinzessin" => "SETTINGS_THEME_PRINCESS",
            "Gelb" => "SETTINGS_THEME_YELLOW",
            "Blau" => "SETTINGS_THEME_BLUE",
            "Grün" => "SETTINGS_THEME_GREEN",
            "Braun" => "SETTINGS_THEME_BROWN",
            "Grau" => "SETTINGS_THEME_GRAY",
            "Weiß" => "SETTINGS_THEME_WHITE",
            "Schwarz" => "SETTINGS_THEME_BLACK",
            _ => "SETTINGS_COLORSCHEME"
        };
    }

    private void LoadLanguageSettings()
    {
        var wasRefreshing = _isRefreshingLocalizedSelections;
        _isRefreshingLocalizedSelections = true;
        try
        {
            var lm = LanguageManager.Instance;
            AvailableLanguages.Clear();
            _languageDisplayToCode.Clear();

            var systemLabel = GetSystemLanguageLabel();
            AvailableLanguages.Add(systemLabel);
            _languageDisplayToCode[systemLabel] = "System";

            foreach (var code in lm.GetAvailableLanguages())
            {
                var selfName = lm.GetLanguageSelfName(code);
                var display = $"{selfName} ({code})";
                AvailableLanguages.Add(display);
                _languageDisplayToCode[display] = code;
            }

            var savedCode = _settingsService.GetLanguageCode();
            var selected = _languageDisplayToCode
                .FirstOrDefault(kv => kv.Value.Equals(savedCode, StringComparison.OrdinalIgnoreCase))
                .Key;

            SelectedLanguage = string.IsNullOrWhiteSpace(selected) ? systemLabel : selected;
        }
        catch
        {
            AvailableLanguages.Clear();
            var systemLabel = GetSystemLanguageLabel();
            AvailableLanguages.Add(systemLabel);
            SelectedLanguage = systemLabel;
        }
        finally
        {
            _isRefreshingLocalizedSelections = wasRefreshing;
        }
    }

    partial void OnSelectedLanguageChanged(string? value)
    {
        if (_isRefreshingLocalizedSelections)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!_languageDisplayToCode.TryGetValue(value, out var code))
        {
            return;
        }

        try
        {
            // Keine Neuinitialisierung auslösen, wenn Sprache effektiv bereits aktiv ist.
            if (code.Equals(LanguageManager.Instance.CurrentLanguageCode, StringComparison.OrdinalIgnoreCase))
            {
                _settingsService.SetLanguageCode(code);
                return;
            }

            _settingsService.SetLanguageCode(code);
            LanguageManager.Instance.LoadLanguage(code);
            RefreshLocalizedSelections(code);
        }
        catch
        {
            // Ignore language persistence/apply errors
        }
    }

    private void RefreshLocalizedSelections(string? selectedLanguageCode = null)
    {
        _isRefreshingLocalizedSelections = true;
        try
        {
            var selectedPrimaryAdapter = SelectedPrimaryAdapter;
            var selectedSecondaryAdapter = SelectedSecondaryAdapter;
            var wasNoAdapterSelected = IsNoAdapterSelection(selectedSecondaryAdapter);
            var selectedThemeName = ResolveSelectedThemeName();
            var selectedCode = string.IsNullOrWhiteSpace(selectedLanguageCode)
                ? ResolveSelectedLanguageCode()
                : selectedLanguageCode;

            LoadAvailableAdapters();
            if (!string.IsNullOrWhiteSpace(selectedPrimaryAdapter) && AvailableAdapters.Contains(selectedPrimaryAdapter))
            {
                SelectedPrimaryAdapter = selectedPrimaryAdapter;
            }

            if (wasNoAdapterSelected)
            {
                SelectedSecondaryAdapter = GetNoAdapterLabel();
            }
            else if (!string.IsNullOrWhiteSpace(selectedSecondaryAdapter) && AvailableSecondaryAdapters.Contains(selectedSecondaryAdapter))
            {
                SelectedSecondaryAdapter = selectedSecondaryAdapter;
            }

            LoadThemeSettings();
            if (!string.IsNullOrWhiteSpace(selectedThemeName) && _themeNameToDisplay.TryGetValue(selectedThemeName, out var themeDisplay))
            {
                SelectedTheme = themeDisplay;
            }

            LoadLanguageSettings();
            if (!string.IsNullOrWhiteSpace(selectedCode))
            {
                var selectedLanguageDisplay = _languageDisplayToCode
                    .FirstOrDefault(kv => kv.Value.Equals(selectedCode, StringComparison.OrdinalIgnoreCase))
                    .Key;

                if (!string.IsNullOrWhiteSpace(selectedLanguageDisplay))
                {
                    SelectedLanguage = selectedLanguageDisplay;
                }
            }

            foreach (var item in DesktopOverlayInfoItems)
            {
                item.DisplayName = GetOverlayItemDisplayName(item.Key);
            }
        }
        finally
        {
            _isRefreshingLocalizedSelections = false;
        }
    }

    private string? ResolveSelectedThemeName()
    {
        if (!string.IsNullOrWhiteSpace(SelectedTheme) && _themeDisplayToName.TryGetValue(SelectedTheme, out var themeName))
        {
            return themeName;
        }

        return _themeService.ReadThemeName();
    }

    private string ResolveSelectedLanguageCode()
    {
        if (!string.IsNullOrWhiteSpace(SelectedLanguage) && _languageDisplayToCode.TryGetValue(SelectedLanguage, out var code))
        {
            return code;
        }

        return _settingsService.GetLanguageCode();
    }

    private static string GetNoAdapterLabel() => T("SETTINGS_ADAPTER_NONE");

    private static bool IsNoAdapterSelection(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return value.Equals(GetNoAdapterLabel(), StringComparison.OrdinalIgnoreCase)
            || value.Equals("(Kein Adapter)", StringComparison.OrdinalIgnoreCase)
            || value.Equals("SETTINGS_ADAPTER_NONE", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSystemLanguageLabel() => $"{T("SETTINGS_LANGUAGE_SYSTEM")} (Auto)";

    /// <summary>
    /// Speichert das ausgewählte Theme.
    /// </summary>
    private void SaveThemeSettings(string themeName)
    {
        try
        {
            _themeService.WriteThemeName(themeName);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Wendet das Theme an.
    /// </summary>
    private void ApplyTheme(string themeName)
    {
        try
        {
            var theme = _themeService.GetThemeByName(themeName);
            ThemeApplier.Apply(theme);
        }
        catch
        {
            // Ignore apply errors
        }
    }

    /// <summary>
    /// Lädt die Verbindungsqualität-Einstellungen (Ping-Schwellwerte).
    /// </summary>
    private void LoadConnectionQualitySettings()
    {
        try
        {
            PingThresholdFast = _settingsService.GetPingThresholdFast();
            PingThresholdNormal = _settingsService.GetPingThresholdNormal();
        }
        catch
        {
            // Defaults
            PingThresholdFast = 20;
            PingThresholdNormal = 50;
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich der Fast-Schwellwert ändert.
    /// </summary>
    partial void OnPingThresholdFastChanged(int value)
    {
        if (value > 0 && value < PingThresholdNormal)
        {
            _settingsService.SetPingThresholdFast(value);
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich der Normal-Schwellwert ändert.
    /// </summary>
    partial void OnPingThresholdNormalChanged(int value)
    {
        if (value > PingThresholdFast)
        {
            _settingsService.SetPingThresholdNormal(value);
        }
    }

    /// <summary>
    /// Lädt die Autostart-Einstellungen.
    /// </summary>
    private void LoadAutostartSettings()
    {
        try
        {
            IsAutostartEnabled = _autostartService.IsAutostartEnabled();
            StartMinimizedToTray = _settingsService.GetStartMinimizedToTray();
        }
        catch
        {
            IsAutostartEnabled = false;
            StartMinimizedToTray = false;
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich die Autostart-Einstellung ändert.
    /// </summary>
    partial void OnIsAutostartEnabledChanged(bool value)
    {
        try
        {
            _autostartService.SetAutostart(value);
        }
        catch
        {
            // Bei Fehler: Wert zurücksetzen
            IsAutostartEnabled = _autostartService.IsAutostartEnabled();
        }
    }
    
    /// <summary>
    /// Wird aufgerufen, wenn sich die "Minimiert starten" Einstellung ändert.
    /// </summary>
    partial void OnStartMinimizedToTrayChanged(bool value)
    {
        try
        {
            _settingsService.SetStartMinimizedToTray(value);
        }
        catch
        {
            // Bei Fehler: Wert zurücksetzen
            StartMinimizedToTray = _settingsService.GetStartMinimizedToTray();
        }
    }

    private void LoadDesktopOverlaySettings()
    {
        _isLoadingDesktopOverlaySettings = true;
        try
        {
            var settings = DesktopOverlaySettingsModel.Normalize(_settingsService.GetDesktopOverlaySettings());

            DesktopOverlayEnabled = settings.Enabled;
            DesktopOverlayHoverInteractive = settings.HoverInteractive;
            DesktopOverlayPositionIndex = settings.PositionMode switch
            {
                DesktopOverlayPositionModes.TopLeft => 0,
                DesktopOverlayPositionModes.TopRight => 1,
                DesktopOverlayPositionModes.BottomLeft => 2,
                DesktopOverlayPositionModes.BottomRight => 3,
                _ => 4
            };

            DesktopOverlayOffsetX = settings.OffsetX;
            DesktopOverlayOffsetY = settings.OffsetY;
            DesktopOverlayWidth = settings.Width;
            DesktopOverlayHeight = settings.Height;
            DesktopOverlayFontSize = (decimal)settings.FontSize;
            DesktopOverlayFontFamily = settings.FontFamily;
            DesktopOverlayTextColor = settings.TextColor;
            DesktopOverlayBackgroundColor = settings.BackgroundColor;
            DesktopOverlayBackgroundOpacityPercent = settings.BackgroundOpacityPercent;
            DesktopOverlayPadding = settings.Padding;
            DesktopOverlayLineSpacing = settings.LineSpacing;
            DesktopOverlayCornerRadius = settings.CornerRadius;
            DesktopOverlayShadowEnabled = settings.ShowShadow;

            DesktopOverlayNetworkRefreshSeconds = settings.NetworkRefreshSeconds;
            DesktopOverlayExternalIpRefreshMinutes = settings.ExternalIpRefreshMinutes;
            DesktopOverlayRamRefreshSeconds = settings.RamRefreshSeconds;
            DesktopOverlayUptimeRefreshSeconds = settings.UptimeRefreshSeconds;

            foreach (var item in DesktopOverlayInfoItems)
            {
                item.PropertyChanged -= OverlayItem_PropertyChanged;
            }

            DesktopOverlayInfoItems.Clear();
            foreach (var item in settings.Items.OrderBy(i => i.Order))
            {
                var vm = new DesktopOverlayItemSettingViewModel(
                    item.Key,
                    GetOverlayItemDisplayName(item.Key),
                    item.IsVisible,
                    item.ShowLabel,
                    item.ShowValue);
                vm.PropertyChanged += OverlayItem_PropertyChanged;
                DesktopOverlayInfoItems.Add(vm);
            }
        }
        catch
        {
            // Ignore loading errors and keep defaults.
        }
        finally
        {
            _isLoadingDesktopOverlaySettings = false;
        }
    }

    private void OverlayItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingDesktopOverlaySettings)
        {
            return;
        }

        if (e.PropertyName is nameof(DesktopOverlayItemSettingViewModel.IsVisible)
            or nameof(DesktopOverlayItemSettingViewModel.ShowLabel)
            or nameof(DesktopOverlayItemSettingViewModel.ShowValue))
        {
            SaveDesktopOverlaySettings();
        }
    }

    public void MoveDesktopOverlayItem(string sourceKey, string targetKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey) || string.IsNullOrWhiteSpace(targetKey))
        {
            return;
        }

        var sourceIndex = DesktopOverlayInfoItems
            .Select((item, index) => new { item, index })
            .FirstOrDefault(x => x.item.Key.Equals(sourceKey, StringComparison.OrdinalIgnoreCase))?.index ?? -1;

        var targetIndex = DesktopOverlayInfoItems
            .Select((item, index) => new { item, index })
            .FirstOrDefault(x => x.item.Key.Equals(targetKey, StringComparison.OrdinalIgnoreCase))?.index ?? -1;

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        var sourceItem = DesktopOverlayInfoItems[sourceIndex];
        DesktopOverlayInfoItems.RemoveAt(sourceIndex);
        if (sourceIndex < targetIndex)
        {
            targetIndex--;
        }

        DesktopOverlayInfoItems.Insert(targetIndex, sourceItem);
        SaveDesktopOverlaySettings();
    }

    private void SaveDesktopOverlaySettings()
    {
        if (_isLoadingDesktopOverlaySettings)
        {
            return;
        }

        try
        {
            var model = new DesktopOverlaySettingsModel
            {
                Enabled = DesktopOverlayEnabled,
                HoverInteractive = DesktopOverlayHoverInteractive,
                PositionMode = DesktopOverlayPositionIndex switch
                {
                    0 => DesktopOverlayPositionModes.TopLeft,
                    1 => DesktopOverlayPositionModes.TopRight,
                    2 => DesktopOverlayPositionModes.BottomLeft,
                    3 => DesktopOverlayPositionModes.BottomRight,
                    _ => DesktopOverlayPositionModes.Free
                },
                OffsetX = (int)DesktopOverlayOffsetX,
                OffsetY = (int)DesktopOverlayOffsetY,
                Width = (int)DesktopOverlayWidth,
                Height = (int)DesktopOverlayHeight,
                FontSize = (double)DesktopOverlayFontSize,
                FontFamily = DesktopOverlayFontFamily,
                TextColor = DesktopOverlayTextColor,
                BackgroundColor = DesktopOverlayBackgroundColor,
                BackgroundOpacityPercent = DesktopOverlayBackgroundOpacityPercent,
                Padding = DesktopOverlayPadding,
                LineSpacing = DesktopOverlayLineSpacing,
                CornerRadius = DesktopOverlayCornerRadius,
                ShowShadow = DesktopOverlayShadowEnabled,
                NetworkRefreshSeconds = DesktopOverlayNetworkRefreshSeconds,
                ExternalIpRefreshMinutes = DesktopOverlayExternalIpRefreshMinutes,
                RamRefreshSeconds = DesktopOverlayRamRefreshSeconds,
                UptimeRefreshSeconds = DesktopOverlayUptimeRefreshSeconds,
                Items = DesktopOverlayInfoItems
                    .Select((item, index) => item.ToModel(index))
                    .ToList()
            };

            _settingsService.SetDesktopOverlaySettings(model);
        }
        catch
        {
            // Ignore persistence errors.
        }
    }

    private static string GetOverlayItemDisplayName(string key)
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

    partial void OnDesktopOverlayEnabledChanged(bool value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayHoverInteractiveChanged(bool value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayPositionIndexChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayOffsetXChanged(decimal value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayOffsetYChanged(decimal value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayWidthChanged(decimal value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayHeightChanged(decimal value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayFontSizeChanged(decimal value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayFontFamilyChanged(string value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayTextColorChanged(string value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayBackgroundColorChanged(string value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayBackgroundOpacityPercentChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayPaddingChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayLineSpacingChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayCornerRadiusChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayShadowEnabledChanged(bool value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayNetworkRefreshSecondsChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayExternalIpRefreshMinutesChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayRamRefreshSecondsChanged(int value) => SaveDesktopOverlaySettings();
    partial void OnDesktopOverlayUptimeRefreshSecondsChanged(int value) => SaveDesktopOverlaySettings();

    public async Task ExportSettingsArchiveAsync(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("Export path must not be empty.", nameof(archivePath));
        }

        await Task.Run(() =>
        {
            var configDir = GetConfigDirectoryPath();
            var targetPath = Path.GetFullPath(archivePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);

            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new InvalidOperationException("Could not resolve target directory for export.");
            }

            Directory.CreateDirectory(targetDirectory);

            using var archive = ZipFile.Open(targetPath, ZipArchiveMode.Create);
            foreach (var filePath in Directory.GetFiles(configDir, "*", SearchOption.TopDirectoryOnly))
            {
                var sourcePath = Path.GetFullPath(filePath);
                if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = Path.GetFileName(filePath);
                archive.CreateEntryFromFile(filePath, fileName, CompressionLevel.Optimal);
            }
        });
    }

    public async Task<string> ExportSettingsArchiveToDefaultLocationAsync()
    {
        var configDir = GetConfigDirectoryPath();
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var archivePath = Path.Combine(configDir, $"neTiPx-config-backup-{timestamp}.zip");
        await ExportSettingsArchiveAsync(archivePath);
        return archivePath;
    }

    public async Task ImportSettingsArchiveAsync(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            throw new ArgumentException("Import path must not be empty.", nameof(archivePath));
        }

        await Task.Run(() =>
        {
            var sourcePath = Path.GetFullPath(archivePath);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Settings archive not found.", sourcePath);
            }

            var configDir = GetConfigDirectoryPath();
            Directory.CreateDirectory(configDir);

            using var archive = ZipFile.OpenRead(sourcePath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue;
                }

                var destinationPath = Path.Combine(configDir, entry.Name);
                entry.ExtractToFile(destinationPath, overwrite: true);
            }
        });

        ReloadSettingsFromDisk();
    }

    public async Task<string?> ResetSettingsDataAsync(string? backupArchivePath = null)
    {
        string? backupPath = null;
        if (!string.IsNullOrWhiteSpace(backupArchivePath))
        {
            backupPath = Path.GetFullPath(backupArchivePath);
            await ExportSettingsArchiveAsync(backupPath);
        }

        await Task.Run(() =>
        {
            var configDir = GetConfigDirectoryPath();
            string? keepPath = null;
            if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
            {
                keepPath = Path.GetFullPath(backupPath);
            }

            foreach (var filePath in Directory.GetFiles(configDir, "*", SearchOption.TopDirectoryOnly))
            {
                var fullPath = Path.GetFullPath(filePath);
                if (!string.IsNullOrWhiteSpace(keepPath) && string.Equals(fullPath, keepPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                File.Delete(filePath);
            }
        });

        ReloadSettingsFromDisk();
        return backupPath;
    }

    private static string GetConfigDirectoryPath()
    {
        var configPath = ConfigFileHelper.GetConfigIniPath();
        var configDir = Path.GetDirectoryName(configPath);

        if (string.IsNullOrWhiteSpace(configDir))
        {
            configDir = AppDomain.CurrentDomain.BaseDirectory;
        }

        Directory.CreateDirectory(configDir);
        return configDir;
    }

    private void ReloadSettingsFromDisk()
    {
        LoadAvailableAdapters();
        LoadAdapterSettings();
        LoadHoverWindowSettings();
        LoadThemeSettings();
        LoadLanguageSettings();
        LoadConnectionQualitySettings();
        LoadAutostartSettings();
    }
}
