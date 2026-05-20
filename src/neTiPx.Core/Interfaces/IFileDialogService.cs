namespace neTiPx.Core.Interfaces;

/// <summary>
/// Datei-Dialog Service für plattformunabhängige Datei-/Ordner-Auswahl
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Zeigt Open File Dialog
    /// </summary>
    Task<string?> ShowOpenFileDialogAsync(string title, FileDialogFilter[]? filters = null);

    /// <summary>
    /// Zeigt Open Multiple Files Dialog
    /// </summary>
    Task<IEnumerable<string>?> ShowOpenMultipleFilesDialogAsync(string title, FileDialogFilter[]? filters = null);

    /// <summary>
    /// Zeigt Save File Dialog
    /// </summary>
    Task<string?> ShowSaveFileDialogAsync(string title, string? defaultFileName = null, FileDialogFilter[]? filters = null);

    /// <summary>
    /// Zeigt Folder Browser Dialog
    /// </summary>
    Task<string?> ShowFolderBrowserDialogAsync(string title);
}

public record FileDialogFilter(string Name, string[] Extensions);
