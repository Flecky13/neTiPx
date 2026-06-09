using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.Core.Models;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Core.Services.AdapterStore _adapterStore;
    private readonly Core.Services.HoverWindowSettings _hoverWindowSettings;
    private readonly Core.Services.ThemeService _themeService;
    private readonly SettingsService _settingsService;
    private readonly AutostartService _autostartService;
    
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
        
        LoadAvailableAdapters();
        LoadAdapterSettings();
        LoadHoverWindowSettings();
        LoadThemeSettings();
        LoadConnectionQualitySettings();
        LoadAutostartSettings();
    }
    
    /// <summary>
    /// Lädt alle verfügbaren Netzwerkadapter.
    /// </summary>
    private void LoadAvailableAdapters()
    {
        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderBy(n => n.Name)
                .Select(n => n.Name)
                .ToList();
            
            AvailableAdapters.Clear();
            foreach (var adapter in adapters)
            {
                AvailableAdapters.Add(adapter);
            }
            
            // Für den sekundären Adapter: Leeren Eintrag am Anfang hinzufügen
            AvailableSecondaryAdapters.Clear();
            AvailableSecondaryAdapters.Add("(Kein Adapter)");
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
                SelectedSecondaryAdapter = "(Kein Adapter)";
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
                    SelectedSecondaryAdapter = "(Kein Adapter)";
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
        SaveAdapterSettings();
    }
    
    /// <summary>
    /// Wird aufgerufen, wenn sich der sekundäre Adapter ändert.
    /// Speichert automatisch die Einstellungen.
    /// </summary>
    partial void OnSelectedSecondaryAdapterChanged(string? value)
    {
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
            if (secondaryAdapter == "(Kein Adapter)")
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
            foreach (var theme in themes)
            {
                AvailableThemes.Add(theme.Name);
            }

            var savedThemeName = _themeService.ReadThemeName();
            SelectedTheme = savedThemeName;
        }
        catch
        {
            // Defaults
            AvailableThemes.Add("Blau");
            SelectedTheme = "Blau";
        }
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich das ausgewählte Theme ändert.
    /// </summary>
    partial void OnSelectedThemeChanged(string? value)
    {
        if (value != null)
        {
            SaveThemeSettings(value);
            ApplyTheme(value);
        }
    }

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
        }
        catch
        {
            IsAutostartEnabled = false;
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
}
