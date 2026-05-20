using neTiPx.Core.Models;

namespace neTiPx.Core.Interfaces;

/// <summary>
/// Netzwerk-Konfiguration Service für plattformspezifische Routen und IP-Konfiguration
/// </summary>
public interface INetworkConfigService
{
    /// <summary>
    /// Gibt alle aktiven Routen zurück
    /// </summary>
    Task<IEnumerable<RouteEntry>> GetRoutesAsync();

    /// <summary>
    /// Fügt eine neue Route hinzu (erfordert evtl. Admin-Rechte)
    /// </summary>
    Task<bool> AddRouteAsync(RouteEntry route);

    /// <summary>
    /// Entfernt eine Route
    /// </summary>
    Task<bool> DeleteRouteAsync(string destination, string? mask = null);

    /// <summary>
    /// Prüft, ob Admin/Root-Rechte benötigt werden
    /// </summary>
    Task<bool> RequiresElevationAsync();

    /// <summary>
    /// Führt Elevation aus (zeigt UAC-Dialog auf Windows)
    /// </summary>
    Task<bool> ElevateAsync();

    /// <summary>
    /// Setzt IP-Adresse für ein Netzwerk-Interface
    /// </summary>
    Task<bool> SetIpAddressAsync(string interfaceName, string ipAddress, string subnetMask);

    /// <summary>
    /// Setzt Gateway für ein Netzwerk-Interface
    /// </summary>
    Task<bool> SetGatewayAsync(string interfaceName, string gateway);

    /// <summary>
    /// Aktiviert DHCP für ein Interface
    /// </summary>
    Task<bool> EnableDhcpAsync(string interfaceName);
}
