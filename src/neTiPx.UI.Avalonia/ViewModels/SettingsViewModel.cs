using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.Core.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AdapterStore _adapterStore;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableAdapters;
    
    [ObservableProperty]
    private ObservableCollection<string> _availableSecondaryAdapters;
    
    [ObservableProperty]
    private string? _selectedPrimaryAdapter;
    
    [ObservableProperty]
    private string? _selectedSecondaryAdapter;
    
    public SettingsViewModel()
    {
        _adapterStore = new AdapterStore();
        _availableAdapters = new ObservableCollection<string>();
        _availableSecondaryAdapters = new ObservableCollection<string>();
        
        LoadAvailableAdapters();
        LoadAdapterSettings();
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
            
            var settings = new AdapterStore.AdapterSettings
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
}
