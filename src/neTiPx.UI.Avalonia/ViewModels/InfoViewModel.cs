using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class InfoViewModel : ObservableObject
{
    private static readonly LanguageManager _lm = LanguageManager.Instance;
    private static string T(string key) => _lm.Lang(key);

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
    private string _assetName = string.Empty;
    
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
        _latestVersion = T("INFO_STATUS_CHECKING");
        _updateStatusText = T("INFO_STATUS_NOT_CHECKED");
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
        LatestVersion = T("INFO_STATUS_CHECKING");
        UpdateStatusText = T("INFO_STATUS_CHECKING");
        UpdateStatusColor = "Orange";
        
        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync(_currentVersion);
            
            if (!string.IsNullOrEmpty(updateInfo.ErrorMessage))
            {
                LatestVersion = T("INFO_UNKNOWN");
                UpdateStatusText = $"{T("INFO_STATUS_CHECK_FAILED")}: {updateInfo.ErrorMessage}";
                UpdateStatusColor = "Red";
                IsUpdateAvailable = false;
                return;
            }
            
            LatestVersion = updateInfo.LatestVersion ?? T("INFO_UNKNOWN");
            DownloadUrl = updateInfo.DownloadUrl ?? string.Empty;
            AssetName = updateInfo.AssetName ?? string.Empty;
            ReleaseUrl = updateInfo.ReleaseUrl ?? string.Empty;
            
            if (updateInfo.IsUpdateAvailable)
            {
                IsUpdateAvailable = true;
                UpdateStatusText = string.IsNullOrWhiteSpace(updateInfo.AssetName)
                    ? T("INFO_STATUS_UPDATE_AVAILABLE")
                    : $"{T("INFO_STATUS_UPDATE_AVAILABLE")} ({updateInfo.AssetName})";
                UpdateStatusColor = "Green";
            }
            else
            {
                IsUpdateAvailable = false;
                UpdateStatusText = T("INFO_STATUS_UP_TO_DATE");
                UpdateStatusColor = "Green";
            }
        }
        catch (Exception ex)
        {
            LatestVersion = T("INFO_UNKNOWN");
            UpdateStatusText = $"{T("INFO_STATUS_CHECK_FAILED")}: {ex.Message}";
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
    private async Task DownloadUpdate()
    {
        if (string.IsNullOrWhiteSpace(DownloadUrl) || IsChecking)
        {
            return;
        }

        IsChecking = true;
        UpdateStatusText = T("INFO_STATUS_DOWNLOADING");
        UpdateStatusColor = "Orange";

        try
        {
            var downloadedPath = await _updateService.DownloadAssetAsync(DownloadUrl, AssetName);
            UpdateStatusText = T("INFO_STATUS_STARTING_INSTALL");
            UpdateStatusColor = "Orange";

            if (TryStartInstaller(downloadedPath, out var shouldShutdownApp))
            {
                if (shouldShutdownApp)
                {
                    await Task.Delay(400);
                    ShutdownApplication(forceCloseMainWindow: true);
                }
                return;
            }

            throw new InvalidOperationException("Installer konnte nicht gestartet werden.");
        }
        catch
        {
            UpdateStatusText = T("INFO_STATUS_DOWNLOAD_FAILED_OPENING_RELEASE");
            UpdateStatusColor = "Red";
            OpenLatestRelease();
        }
        finally
        {
            IsChecking = false;
        }
    }

    private bool CanDownloadUpdate() => IsUpdateAvailable && !string.IsNullOrEmpty(DownloadUrl);

    private bool TryStartInstaller(string packagePath, out bool shouldShutdownApp)
    {
        shouldShutdownApp = false;

        if (!File.Exists(packagePath))
        {
            return false;
        }

        var extension = Path.GetExtension(packagePath).ToLowerInvariant();

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = packagePath,
                    UseShellExecute = true
                });
                shouldShutdownApp = true;
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (extension == ".appimage")
                {
                    Process.Start("chmod", $"+x \"{packagePath}\"")?.Dispose();
                }

                if (extension == ".deb")
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "pkexec",
                            Arguments = $"dpkg -i \"{packagePath}\"",
                            UseShellExecute = false
                        });
                        // Unter Linux nicht sofort beenden, sonst verschwindet die App,
                        // bevor die Rechte-Eingabe sauber abgeschlossen ist.
                        shouldShutdownApp = false;
                        return true;
                    }
                    catch
                    {
                        // Fallback below.
                    }
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    Arguments = $"\"{packagePath}\"",
                    UseShellExecute = false
                });
                shouldShutdownApp = false;
                return true;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{packagePath}\"",
                    UseShellExecute = false
                });
                shouldShutdownApp = true;
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static void ShutdownApplication(bool forceCloseMainWindow = false)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            if (forceCloseMainWindow && lifetime.MainWindow is Views.MainWindow)
            {
                Views.MainWindow.AllowCloseOnce();
            }

            lifetime.Shutdown();
        }
    }

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

    partial void OnDownloadUrlChanged(string value)
    {
        DownloadUpdateCommand.NotifyCanExecuteChanged();
    }
}
