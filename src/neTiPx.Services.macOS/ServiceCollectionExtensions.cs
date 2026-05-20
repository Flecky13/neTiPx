using Microsoft.Extensions.DependencyInjection;
using neTiPx.Core.Interfaces;

namespace neTiPx.Services.macOS;

/// <summary>
/// Service-Registrierung für macOS-spezifische Services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registriert macOS-spezifische Service-Implementierungen
    /// </summary>
    public static IServiceCollection AddMacOSServices(this IServiceCollection services)
    {
        // macOS-spezifische Services registrieren
        // services.AddSingleton<IWifiNetworkService, WifiNetworkServiceMacOS>();
        // services.AddSingleton<ITrayService, TrayServiceMacOS>();
        // services.AddSingleton<IAutoStartService, AutoStartServiceMacOS>();
        // services.AddSingleton<INetworkConfigService, NetworkConfigServiceMacOS>();
        // services.AddSingleton<IFileDialogService, FileDialogServiceMacOS>();
        // services.AddSingleton<IProcessService, ProcessServiceMacOS>();

        return services;
    }
}
