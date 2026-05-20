using Microsoft.Extensions.DependencyInjection;
using neTiPx.Core.Interfaces;

namespace neTiPx.Services.Linux;

/// <summary>
/// Service-Registrierung für Linux-spezifische Services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registriert Linux-spezifische Service-Implementierungen
    /// </summary>
    public static IServiceCollection AddLinuxServices(this IServiceCollection services)
    {
        // Linux-spezifische Services registrieren
        // services.AddSingleton<IWifiNetworkService, WifiNetworkServiceLinux>();
        // services.AddSingleton<ITrayService, TrayServiceLinux>();
        // services.AddSingleton<IAutoStartService, AutoStartServiceLinux>();
        // services.AddSingleton<INetworkConfigService, NetworkConfigServiceLinux>();
        // services.AddSingleton<IFileDialogService, FileDialogServiceLinux>();
        // services.AddSingleton<IProcessService, ProcessServiceLinux>();

        return services;
    }
}
