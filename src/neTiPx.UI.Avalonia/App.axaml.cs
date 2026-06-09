using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Services;
using neTiPx.UI.Avalonia.ViewModels;
using neTiPx.UI.Avalonia.Views;
using System;
using System.Linq;
using System.Runtime.InteropServices;

#if WINDOWS
using neTiPx.Services.Windows;
#elif LINUX || UNIX
using neTiPx.Services.Linux;
#elif OSX
using neTiPx.Services.macOS;
#endif

namespace neTiPx.UI.Avalonia;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }
    private TrayService? _trayService;
    public static TrayService? TrayService => ((App?)Current)?._trayService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Load language before anything else
            LoadAndApplyLanguage();
            
            // Load and apply saved theme before creating window
            LoadAndApplyTheme();
            
            // Prüfe auf --minimized Parameter und User-Einstellung
            var args = Environment.GetCommandLineArgs();
            bool hasMinimizedParam = args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            
            var settingsService = new SettingsService();
            bool startMinimizedSetting = settingsService.GetStartMinimizedToTray();
            
            // Nur minimiert starten, wenn BEIDE Bedingungen erfüllt sind
            bool startMinimized = hasMinimizedParam && startMinimizedSetting;
            
            // Setze ShutdownMode auf ExplicitShutdown, damit die App nicht beendet wird, wenn kein Fenster sichtbar ist
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            // Erstelle MainWindow
            var mainWindow = new MainWindow();
            
            // Weise das Fenster zu
            desktop.MainWindow = mainWindow;
            
            // Initialize TrayService
            _trayService = new TrayService();
            
            if (startMinimized)
            {
                // Bei minimized: Verzögert verstecken (nach vollständiger Initialisierung)
                mainWindow.ShowInTaskbar = false;
                Dispatcher.UIThread.Post(() => 
                {
                    desktop.MainWindow?.Hide();
                }, DispatcherPriority.Loaded);
            }
            else
            {
                // Normal starten: Fenster anzeigen
                desktop.MainWindow.Show();
            }
            
            desktop.ShutdownRequested += (sender, e) =>
            {
                _trayService?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core Services registrieren
        services.AddCoreServices();

        // Plattformspezifische Services registrieren
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            #if WINDOWS
            services.AddWindowsServices();
            #endif
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            #if LINUX || UNIX
            services.AddLinuxServices();
            #endif
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            #if OSX
            services.AddMacOSServices();
            #endif
        }

        // ViewModels registrieren (später)
        // services.AddTransient<MainWindowViewModel>();

        ServiceProvider = services.BuildServiceProvider();
    }

    private void LoadAndApplyLanguage()
    {
        try
        {
            var settingsService = new SettingsService();
            var languageCode = settingsService.GetLanguageCode() ?? "System";
            LanguageManager.Instance.LoadLanguage(languageCode);
        }
        catch
        {
            // Fallback to system language on error
            LanguageManager.Instance.LoadLanguage("System");
        }
    }

    private void LoadAndApplyTheme()
    {
        try
        {
            var themeService = new ThemeService();
            var themeName = themeService.ReadThemeName();
            var theme = themeService.GetThemeByName(themeName);
            ThemeApplier.Apply(theme);
        }
        catch
        {
            // Ignore theme loading errors, use default
        }
    }
}
