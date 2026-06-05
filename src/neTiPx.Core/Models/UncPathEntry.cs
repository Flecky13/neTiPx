using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Einzelner UNC-Pfad-Eintrag mit Authentifizierung
/// </summary>
public sealed partial class UncPathEntry : ObservableObject
{
    [ObservableProperty]
    private string _uncPath = string.Empty;
    
    [ObservableProperty]
    private string _username = string.Empty;
    
    [ObservableProperty]
    private string _password = string.Empty;
    
    [ObservableProperty]
    private string _driveLetter = string.Empty;
    
    [ObservableProperty]
    private bool _hasUncPathError;
    
    [ObservableProperty]
    private bool _canRemove = true;

    /// <summary>
    /// Validiert den UNC-Pfad (einfache Prüfung auf \\server\share Format)
    /// </summary>
    public bool IsValid()
    {
        return ValidateUncPath(UncPath);
    }

    /// <summary>
    /// Validiert UNC-Pfad-Format
    /// </summary>
    public static bool ValidateUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true; // Leer ist erlaubt

        // UNC-Pfad muss mit \\ beginnen
        if (!path.StartsWith("\\\\", StringComparison.OrdinalIgnoreCase))
            return false;

        // Sollte mindestens \\server\share haben
        var parts = path.Split('\\', System.StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2;
    }
}
