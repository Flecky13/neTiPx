using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            
            desktop.MainWindow = new MainWindow();
            
            // Initialize TrayService
            _trayService = new TrayService();
            
            // Prüfe auf --minimized Parameter
            var args = Environment.GetCommandLineArgs();
            bool startMinimized = args.Any(arg => arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
            
            if (!startMinimized)
            {
                // Nur anzeigen, wenn nicht minimiert gestartet werden soll
                desktop.MainWindow.Show();
            }
            // Wenn minimiert gestartet, bleibt das Fenster versteckt (nur im Tray)
            
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
