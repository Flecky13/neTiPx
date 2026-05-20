namespace neTiPx.Core.Interfaces;

/// <summary>
/// Autostart Service für plattformspezifische Autostart-Verwaltung
/// </summary>
public interface IAutoStartService
{
    /// <summary>
    /// Prüft, ob Autostart aktiviert ist
    /// </summary>
    Task<bool> IsEnabledAsync();

    /// <summary>
    /// Aktiviert oder deaktiviert Autostart
    /// </summary>
    Task<bool> SetEnabledAsync(bool enabled);

    /// <summary>
    /// Gibt den Pfad zur ausführbaren Datei zurück
    /// </summary>
    Task<string> GetExecutablePathAsync();

    /// <summary>
    /// Setzt Autostart-Parameter (z.B. --minimized)
    /// </summary>
    Task<bool> SetArgumentsAsync(string arguments);

    /// <summary>
    /// Gibt die aktuellen Autostart-Parameter zurück
    /// </summary>
    Task<string?> GetArgumentsAsync();
}
