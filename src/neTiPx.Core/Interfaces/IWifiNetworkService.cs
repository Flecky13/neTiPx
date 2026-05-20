namespace neTiPx.Core.Interfaces;

/// <summary>
/// WiFi Network Service für plattformunabhängiges WiFi-Scanning und -Management
/// </summary>
public interface IWifiNetworkService
{
    /// <summary>
    /// Scannt verfügbare WiFi-Netzwerke
    /// </summary>
    Task<IEnumerable<WifiNetworkInfo>> ScanNetworksAsync();

    /// <summary>
    /// Gibt die aktuelle WiFi-Verbindung zurück
    /// </summary>
    Task<WifiConnectionInfo?> GetCurrentConnectionAsync();

    /// <summary>
    /// Verbindet mit einem WiFi-Netzwerk
    /// </summary>
    Task<bool> ConnectAsync(string ssid, string password);

    /// <summary>
    /// Trennt die aktuelle WiFi-Verbindung
    /// </summary>
    Task<bool> DisconnectAsync();

    /// <summary>
    /// Prüft, ob WiFi verfügbar ist
    /// </summary>
    Task<bool> IsAvailableAsync();
}

public record WifiNetworkInfo(
    string Ssid,
    int SignalStrength,
    string SecurityType,
    string? Bssid = null);

public record WifiConnectionInfo(
    string Ssid,
    int SignalStrength,
    string IpAddress,
    string Gateway);
