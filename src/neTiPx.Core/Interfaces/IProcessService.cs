namespace neTiPx.Core.Interfaces;

/// <summary>
/// Prozess-Ausführungs Service für plattformunabhängige Command-Line-Ausführung
/// </summary>
public interface IProcessService
{
    /// <summary>
    /// Führt einen Befehl aus und wartet auf Beendigung
    /// </summary>
    Task<ProcessResult> ExecuteAsync(string fileName, string arguments, bool asAdmin = false);

    /// <summary>
    /// Führt einen Befehl aus und gibt die Ausgabe zurück
    /// </summary>
    Task<ProcessResult> ExecuteWithOutputAsync(string fileName, string arguments, bool asAdmin = false);

    /// <summary>
    /// Öffnet eine URL im Standard-Browser
    /// </summary>
    Task OpenUrlAsync(string url);

    /// <summary>
    /// Öffnet eine Datei mit der Standardanwendung
    /// </summary>
    Task OpenFileAsync(string filePath);

    /// <summary>
    /// Öffnet einen Ordner im Datei-Explorer
    /// </summary>
    Task OpenFolderAsync(string folderPath);

    /// <summary>
    /// Prüft, ob ein Programm verfügbar ist
    /// </summary>
    Task<bool> IsProgramAvailableAsync(string programName);
}

public record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool Success);
