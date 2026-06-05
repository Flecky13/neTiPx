namespace neTiPx.Core.Models;

/// <summary>
/// Gateway-Status für Netzwerk-Monitoring
/// </summary>
public enum GatewayStatusKind
{
    Unknown,
    Reachable,
    Unreachable,
    Checking
}

/// <summary>
/// Route-Modus (wird evtl. erweitert)
/// </summary>
public enum RouteMode
{
    Manual,
    ProfileBased
}

/// <summary>
/// Recent File Entry für zuletzt verwendete Dateien
/// </summary>
public record RecentFileEntry(
    string FilePath,
    DateTime LastAccessed,
    string? DisplayName = null);

// MountedUncConnection is now in its own file (MountedUncConnection.cs)
