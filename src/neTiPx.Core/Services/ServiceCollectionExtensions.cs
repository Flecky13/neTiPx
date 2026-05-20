using Microsoft.Extensions.DependencyInjection;
using neTiPx.Core.Interfaces;

namespace neTiPx.Core.Services;

/// <summary>
/// Service-Registrierung für Core-Services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registriert alle Core-Services (plattformunabhängig)
    /// </summary>
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        // Plattformunabhängige Services hier registrieren
        // z.B. Config-Store, Language Manager, etc.
        
        return services;
    }

    /// <summary>
    /// Registriert plattformspezifische Services
    /// Muss von den jeweiligen Platform-Projekten überschrieben werden
    /// </summary>
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        // Diese Methode wird von den plattformspezifischen Extension-Klassen erweitert
        // Beispiel:
        // - neTiPx.Services.Windows.ServiceCollectionExtensions.AddWindowsServices()
        // - neTiPx.Services.Linux.ServiceCollectionExtensions.AddLinuxServices()
        // - neTiPx.Services.macOS.ServiceCollectionExtensions.AddMacOSServices()
        
        return services;
    }
}
