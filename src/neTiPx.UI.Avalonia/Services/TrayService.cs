using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using neTiPx.Core.Models;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Helpers;
using neTiPx.UI.Avalonia.Views;
using Timer = System.Timers.Timer;

namespace neTiPx.UI.Avalonia.Services;

public class TrayService : IDisposable
{
    private readonly TrayIcon _trayIcon;
    private readonly HoverWindow _hoverWindow;
    private readonly Timer _autoHideTimer;
    private readonly Timer _singleClickTimer;
    private readonly IpProfileStore _ipProfileStore;
    private readonly NetworkConfigService _networkConfigService;
    private int _clickCount = 0;
    private const int DoubleClickMilliseconds = 500;
    private const int AutoHideSeconds = 5;
    private bool _isHoverWindowVisible = false;
    private double? _cachedWindowHeight = null;

    public TrayService()
    {
        _hoverWindow = new HoverWindow();
        _ipProfileStore = new IpProfileStore();
        _networkConfigService = new NetworkConfigService();

        _autoHideTimer = new Timer(AutoHideSeconds * 1000) { AutoReset = false };
        _autoHideTimer.Elapsed += AutoHideTimer_Elapsed;

        _singleClickTimer = new Timer(DoubleClickMilliseconds) { AutoReset = false };
        _singleClickTimer.Elapsed += SingleClickTimer_Elapsed;

        // Create TrayIcon
        _trayIcon = new TrayIcon();
        
        // Load icon from assets
        try
        {
            var iconUri = new Uri("avares://neTiPx.UI.Avalonia/Assets/toolicon.ico");
            var assets = AssetLoader.Open(iconUri);
            _trayIcon.Icon = new WindowIcon(assets);
        }
        catch
        {
            // Fallback: no icon
        }

        _trayIcon.ToolTipText = "neTiPx";
        
        // Create context menu
        BuildTrayMenu();
        
        // Handle clicks - detect single vs double click
        _trayIcon.Clicked += OnTrayIconClicked;
        
        _trayIcon.IsVisible = true;
    }

    private void BuildTrayMenu()
    {
        var menu = new NativeMenu();
        
        var infoItem = new NativeMenuItem("Netzwerk-Info umschalten");
        infoItem.Click += (_, _) => ToggleHoverWindow();
        menu.Add(infoItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        var openItem = new NativeMenuItem("Öffnen/Schließen");
        openItem.Click += (_, _) => ToggleMainWindow();
        menu.Add(openItem);
        
        menu.Add(new NativeMenuItemSeparator());
        
        // IP-Profile submenu
        AddIpProfilesMenu(menu);
        
        menu.Add(new NativeMenuItemSeparator());
        
        var exitItem = new NativeMenuItem("Beenden");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Add(exitItem);
        
        _trayIcon.Menu = menu;
    }

    /// <summary>
    /// Aktualisiert das Tray-Menü (z.B. nach Änderungen an IP-Profilen)
    /// </summary>
    public void RefreshMenu()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            BuildTrayMenu();
        });
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        _clickCount++;
        
        if (_clickCount == 1)
        {
            // Start timer to wait for potential second click
            _singleClickTimer.Stop();
            _singleClickTimer.Start();
        }
        else if (_clickCount == 2)
        {
            // Double click detected - stop timer and toggle main window
            _singleClickTimer.Stop();
            _clickCount = 0;
            ToggleMainWindow();
        }
    }

    private void SingleClickTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        // Timer elapsed, it was a single click - toggle info window
        _clickCount = 0;
        ToggleHoverWindow();
    }

    private void ToggleHoverWindow()
    {
        if (_isHoverWindowVisible)
        {
            // Window is open - close it
            HideHoverWindow();
        }
        else
        {
            // Window is closed - open it
            ShowHoverWindow();
        }
    }

    private async Task ShowHoverWindowAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await _hoverWindow.RefreshAsync();
            
            // Load settings and position window according to configuration
            var settingsService = new HoverWindowSettings();
            var settings = settingsService.ReadSettings();
            
            // Get or measure window size
            double windowWidth = 330; // Fixed width from AXAML
            double windowHeight;
            
            // If we don't have cached height, measure it
            if (_cachedWindowHeight == null)
            {
                // Try to measure without showing
                _hoverWindow.Measure(Size.Infinity);
                windowHeight = _hoverWindow.DesiredSize.Height;
                
                // If measure didn't work (returns 0), use fallback
                if (windowHeight <= 0)
                {
                    windowHeight = 450; // Fallback height
                }
                
                _cachedWindowHeight = windowHeight;
            }
            else
            {
                windowHeight = _cachedWindowHeight.Value;
            }
            
            // Calculate position based on settings
            var screen = _hoverWindow.Screens.Primary;
            if (screen != null)
            {
                var workingArea = screen.WorkingArea;
                
                // Calculate X position (from right edge, horizontal offset)
                var x = workingArea.Right - windowWidth - settings.RightOffsetPixels;
                
                // Calculate Y position based on anchor (vertical offset)
                double y;
                if (settings.VerticalAnchor == "Top")
                {
                    // Position from top edge + vertical offset
                    y = workingArea.Y + settings.VerticalOffsetPixels;
                }
                else
                {
                    // Position from bottom edge - window height - vertical offset
                    y = workingArea.Y + workingArea.Height - windowHeight - settings.VerticalOffsetPixels;
                }
                
                // Set position BEFORE showing
                _hoverWindow.Position = new PixelPoint((int)x, (int)y);
            }

            // Show the window at the correct position
            _hoverWindow.Show();
            _isHoverWindowVisible = true;

            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        });
    }

    private void ShowHoverWindow()
    {
        _ = ShowHoverWindowAsync();
    }

    private void ToggleMainWindow()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    if (mainWindow.IsVisible)
                    {
                        // Window is open - hide it
                        mainWindow.Hide();
                    }
                    else
                    {
                        // Window is closed - show it
                        mainWindow.Show();
                        mainWindow.Activate();
                        mainWindow.WindowState = WindowState.Normal;
                    }
                }
            }
        });
    }

    private void ExitApplication()
    {
        Dispose();
        
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        });
    }

    private void HideHoverWindow()
    {
        _autoHideTimer.Stop();
        _isHoverWindowVisible = false;
        Dispatcher.UIThread.InvokeAsync(() => _hoverWindow.Hide());
    }

    private void AutoHideTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        HideHoverWindow();
    }

    private void AddIpProfilesMenu(NativeMenu parentMenu)
    {
        try
        {
            // Load all IP profiles
            var profiles = _ipProfileStore.ReadAllProfiles();
            
            LogHandler.LogSystemMessage(LogLevel.INFO, "TrayService", $"IP-Profile geladen: {profiles.Count}");
            
            // Always add the menu item, even if no profiles exist
            var profilesSubmenu = new NativeMenu();
            
            if (profiles.Count == 0)
            {
                // Show a message that no profiles exist
                var noProfilesItem = new NativeMenuItem("(Keine Profile vorhanden)");
                profilesSubmenu.Add(noProfilesItem);
            }
            else
            {
                // Add all profiles to submenu
                foreach (var profile in profiles)
                {
                    // Format: "Name (Adapter)" oder nur "Name" wenn kein Adapter
                    var displayName = profile.Name;
                    if (!string.IsNullOrWhiteSpace(profile.AdapterName))
                    {
                        displayName = $"{profile.Name} ({profile.AdapterName})";
                    }
                    
                    var profileItem = new NativeMenuItem(displayName);
                    
                    // Capture profile name for the click handler
                    var profileName = profile.Name;
                    profileItem.Click += (_, _) => ApplyIpProfile(profileName);
                    
                    profilesSubmenu.Add(profileItem);
                    LogHandler.LogSystemMessage(LogLevel.INFO, "TrayService", $"Profil hinzugefügt: '{displayName}'");
                }
            }

            // Create parent menu item with submenu
            var profilesParentItem = new NativeMenuItem("IP-Profile")
            {
                Menu = profilesSubmenu
            };
            
            parentMenu.Add(profilesParentItem);
        }
        catch (Exception ex)
        {
            LogHandler.LogSystemMessage(LogLevel.ERROR, "TrayService", $"Fehler beim Laden der IP-Profile: {ex.Message}");
        }
    }

    private void ApplyIpProfile(string profileName)
    {
        try
        {
            LogHandler.LogSystemMessage(LogLevel.INFO, "TrayService", $"IP-Profil anwenden aus Tray: '{profileName}'");
            
            // Load the profile
            if (!_ipProfileStore.TryGetProfile(profileName, out var profile))
            {
                LogHandler.LogSystemMessage(LogLevel.WARN, "TrayService", $"Profil nicht gefunden: '{profileName}'");
                return;
            }

            // Apply the profile
            var (success, error) = _networkConfigService.ApplyProfile(profile);
            
            if (!success)
            {
                LogHandler.LogSystemMessage(LogLevel.ERROR, "TrayService", $"Fehler beim Anwenden des Profils '{profileName}': {error}");
                // TODO: Show notification to user
            }
            else
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "TrayService", $"Profil '{profileName}' erfolgreich angewendet");
                // TODO: Show success notification
            }
        }
        catch (Exception ex)
        {
            LogHandler.LogSystemMessage(LogLevel.ERROR, "TrayService", $"Ausnahme beim Anwenden des Profils '{profileName}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        _trayIcon.IsVisible = false;
        _trayIcon.Dispose();
        _autoHideTimer.Dispose();
        _singleClickTimer.Dispose();
        _hoverWindow?.Close();
    }
}
