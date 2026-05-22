using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neTiPx.Core.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class InfoViewModel : ObservableObject
{
    private readonly GitHubUpdateService _updateService;
    private Version? _currentVersion;
    
    [ObservableProperty]
    private string _appVersion;

    [ObservableProperty]
    private string _latestVersion;

    [ObservableProperty]
    private string _updateStatusText;
    
    [ObservableProperty]
    private string _updateStatusColor;
    
    [ObservableProperty]
    private bool _isUpdateAvailable;
    
    [ObservableProperty]
    private bool _isChecking;
    
    [ObservableProperty]
    private string _downloadUrl = string.Empty;
    
    [ObservableProperty]
    private string _releaseUrl = string.Empty;
    
    [ObservableProperty]
    private string _platformInfo;

    public InfoViewModel()
    {
        _updateService = new GitHubUpdateService();
        
        // Version aus der Assembly auslesen
        _currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        _appVersion = _currentVersion?.ToString(4) ?? "0.0.0.0";
        _latestVersion = "Wird geprüft...";
        _updateStatusText = "Noch nicht geprüft";
        _updateStatusColor = "Gray";
        _platformInfo = _updateService.GetPlatformDisplayName();
        
        // Automatisch beim Start prüfen
        _ = CheckForUpdatesAsync();
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsChecking || _currentVersion == null)
            return;
        
        IsChecking = true;
        LatestVersion = "Wird geprüft...";
        UpdateStatusText = "Prüfe GitHub Releases...";
        UpdateStatusColor = "Orange";
        
        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync(_currentVersion);
            
            if (!string.IsNullOrEmpty(updateInfo.ErrorMessage))
            {
                LatestVersion = "Fehler";
                UpdateStatusText = updateInfo.ErrorMessage;
                UpdateStatusColor = "Red";
                IsUpdateAvailable = false;
                return;
            }
            
            LatestVersion = updateInfo.LatestVersion ?? "Unbekannt";
            DownloadUrl = updateInfo.DownloadUrl ?? string.Empty;
            ReleaseUrl = updateInfo.ReleaseUrl ?? string.Empty;
            
            if (updateInfo.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                UpdateStatusText = $"Neue Version verfügbar! ({updateInfo.AssetName})";
                UpdateStatusColor = "Green";
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusText = "Sie verwenden die neueste Version";
                UpdateStatusColor = "Green";
            }
        }
        catch (Exception ex)
        {
            LatestVersion = "Fehler";
            UpdateStatusText = $"Fehler: {ex.Message}";
            UpdateStatusColor = "Red";
            IsUpdateAvailable = false;
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private void OpenGitHubReleases()
    {
        OpenUrl($"https://github.com/Flecky13/neTiPx/releases");
    }

    [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
    private void DownloadUpdate()
    {
        if (!string.IsNullOrEmpty(DownloadUrl))
        {
            OpenUrl(DownloadUrl);
        }
    }

    private bool CanDownloadUpdate() => IsUpdateAvailable && !string.IsNullOrEmpty(DownloadUrl);

    [RelayCommand]
    private void OpenLatestRelease()
    {
        if (!string.IsNullOrEmpty(ReleaseUrl))
        {
            OpenUrl(ReleaseUrl);
        }
        else
        {
            OpenUrl($"https://github.com/Flecky13/neTiPx/releases/latest");
        }
    }

    [RelayCommand]
    private void OpenDocumentation()
    {
        OpenUrl("https://github.com/Flecky13/neTiPx#readme");
    }

    [RelayCommand]
    private void OpenBugReport()
    {
        OpenUrl("https://github.com/Flecky13/neTiPx/issues/new");
    }

    [RelayCommand]
    private void OpenGitHubRepository()
    {
        OpenUrl("https://github.com/Flecky13/neTiPx");
    }

    [RelayCommand]
    private void OpenContact()
    {
        OpenUrl("https://github.com/Flecky13");
    }

    [RelayCommand]
    private void OpenSupport()
    {
        OpenUrl("https://buymeacoffee.com/pedrotepe");
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Fehler beim Öffnen der URL - könnte hier ein Notification-System nutzen
            Debug.WriteLine($"Fehler beim Öffnen der URL: {ex.Message}");
        }
    }
    
    partial void OnIsUpdateAvailableChanged(bool value)
    {
        // Command CanExecute aktualisieren
        DownloadUpdateCommand.NotifyCanExecuteChanged();
    }
}
