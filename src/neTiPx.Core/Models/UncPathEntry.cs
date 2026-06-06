using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Einzelner UNC-Pfad-Eintrag mit Authentifizierung (Cross-Platform)
/// </summary>
public sealed partial class UncPathEntry : ObservableObject
{
    [ObservableProperty]
    private string _uncPath = string.Empty;
    
    [ObservableProperty]
    private string _username = string.Empty;
    
    [ObservableProperty]
    private string _password = string.Empty;
    
    /// <summary>
    /// Windows: Laufwerksbuchstabe (z.B. "Z:")
    /// </summary>
    [ObservableProperty]
    private string _driveLetter = string.Empty;
    
    /// <summary>
    /// Linux/macOS: Mount-Point Pfad (z.B. "/mnt/share" oder "/Volumes/share")
    /// </summary>
    [ObservableProperty]
    private string _mountPoint = string.Empty;
    
    [ObservableProperty]
    private bool _hasUncPathError;
    
    [ObservableProperty]
    private bool _canRemove = true;

    /// <summary>
    /// Validiert den UNC-Pfad (einfache Prüfung auf \\server\share oder //server/share Format)
    /// </summary>
    public bool IsValid()
    {
        return ValidateUncPath(UncPath);
    }

    /// <summary>
    /// Validiert UNC-Pfad-Format (unterstützt Windows und Unix-Notation)
    /// </summary>
    public static bool ValidateUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true; // Leer ist erlaubt

        // Windows UNC-Pfad: \\server\share
        if (path.StartsWith("\\\\", StringComparison.OrdinalIgnoreCase))
        {
            var parts = path.Split('\\', System.StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2;
        }

        // Unix/SMB UNC-Pfad: //server/share
        if (path.StartsWith("//", StringComparison.OrdinalIgnoreCase))
        {
            var parts = path.Split('/', System.StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2;
        }

        return false;
    }

    /// <summary>
    /// Konvertiert Windows UNC-Pfad zu Unix-Notation (\\server\share -> //server/share)
    /// </summary>
    public string ToUnixPath()
    {
        if (string.IsNullOrWhiteSpace(UncPath))
            return string.Empty;

        return UncPath.Replace('\\', '/');
    }

    /// <summary>
    /// Konvertiert Unix UNC-Pfad zu Windows-Notation (//server/share -> \\server\share)
    /// </summary>
    public string ToWindowsPath()
    {
        if (string.IsNullOrWhiteSpace(UncPath))
            return string.Empty;

        return UncPath.Replace('/', '\\');
    }
}
