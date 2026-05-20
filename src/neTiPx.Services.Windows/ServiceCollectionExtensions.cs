using Microsoft.Extensions.DependencyInjection;
using neTiPx.Core.Interfaces;

namespace neTiPx.Services.Windows;

/// <summary>
/// Service-Registrierung für Windows-spezifische Services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registriert Windows-spezifische Service-Implementierungen
    /// </summary>
    public static IServiceCollection AddWindowsServices(this IServiceCollection services)
    {
        // Windows-spezifische Services registrieren
        // services.AddSingleton<IWifiNetworkService, WifiNetworkServiceWindows>();
        // services.AddSingleton<ITrayService, TrayServiceWindows>();
        // services.AddSingleton<IAutoStartService, AutoStartServiceWindows>();
        // services.AddSingleton<INetworkConfigService, NetworkConfigServiceWindows>();
        // services.AddSingleton<IFileDialogService, FileDialogServiceWindows>();
        // services.AddSingleton<IProcessService, ProcessServiceWindows>();

        return services;
    }
}
