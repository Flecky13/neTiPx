using System.ComponentModel.DataAnnotations;
using neTiPx.Helpers;

namespace neTiPx.Models;

/// <summary>
/// Einzelner UNC-Pfad-Eintrag mit Authentifizierung
/// </summary>
public sealed class UncPathEntry : ObservableObject
{
    private string _uncPath = string.Empty;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _hasUncPathError;
    private bool _canRemove = true;

    /// <summary>
    /// UNC-Pfad (z.B. \\server\share)
    /// </summary>
    public string UncPath
    {
        get => _uncPath;
        set => SetProperty(ref _uncPath, value);
    }

    /// <summary>
    /// Benutzername für die Authentifizierung
    /// </summary>
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    /// <summary>
    /// Passwort für die Authentifizierung
    /// </summary>
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    /// <summary>
    /// Validierungsfehler für UNC-Pfad
    /// </summary>
    public bool HasUncPathError
    {
        get => _hasUncPathError;
        set => SetProperty(ref _hasUncPathError, value);
    }

    /// <summary>
    /// Gibt an, ob dieser Eintrag gelöscht werden kann
    /// </summary>
    public bool CanRemove
    {
        get => _canRemove;
        set => SetProperty(ref _canRemove, value);
    }

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
