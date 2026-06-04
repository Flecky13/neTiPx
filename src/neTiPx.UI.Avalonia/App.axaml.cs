using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using neTiPx.Core.Services;
using neTiPx.UI.Avalonia.Services;
using neTiPx.UI.Avalonia.Views;
using System;
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

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            
            // Initialize TrayService
            _trayService = new TrayService();
            
            // Start minimized to tray (window is created but not shown)
            // User can open it from tray icon
            
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
}
