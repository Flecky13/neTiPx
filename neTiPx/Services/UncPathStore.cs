using System.Collections.ObjectModel;
using System.Xml.Linq;
using neTiPx.Models;

namespace neTiPx.Services;

/// <summary>
/// Verwaltet die Persistierung von UNC-Pfad-Profilen in XML
/// </summary>
public sealed class UncPathStore
{
    private readonly string _profilesPath;

    public UncPathStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neTiPx"
        );

        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);

        _profilesPath = Path.Combine(appDataPath, "UNC-Profile.xml");
    }

    /// <summary>
    /// Liest alle UNC-Pfad-Profile aus der Konfigurationsdatei
    /// </summary>
    public ObservableCollection<UncPathProfile> LoadProfiles()
    {
        var profiles = new ObservableCollection<UncPathProfile>();

        try
        {
            if (!File.Exists(_profilesPath))
                return profiles;

            var doc = XDocument.Load(_profilesPath);
            var root = doc.Root;

            if (root?.Name.LocalName != "uncProfiles")
                return profiles;

            foreach (var profileElement in root.Elements("profile"))
            {
                var profile = new UncPathProfile
                {
                    Name = profileElement.Attribute("name")?.Value ?? "Unnamed"
                };

                // UNC-Pfade laden
                foreach (var pathElement in profileElement.Elements("uncPath"))
                {
                    var rawDriveLetter = pathElement.Attribute("driveLetter")?.Value ?? string.Empty;
                    var normalizedDrive = NormalizeDriveLetter(rawDriveLetter);

                    var entry = new UncPathEntry
                    {
                        UncPath = pathElement.Attribute("path")?.Value ?? string.Empty,
                        Username = pathElement.Attribute("username")?.Value ?? string.Empty,
                        Password = pathElement.Attribute("password")?.Value ?? string.Empty,
                        DriveLetter = normalizedDrive
                    };

                    profile.UncPaths.Add(entry);
                }

                profiles.Add(profile);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UncPathStore] Fehler beim Laden der Profile: {ex.Message}");
        }

        return profiles;
    }

    /// <summary>
    /// Speichert alle Profile in der XML-Datei
    /// </summary>
    public bool SaveProfiles(IEnumerable<UncPathProfile> profiles)
    {
        try
        {
            var root = new XElement("uncProfiles");

            foreach (var profile in profiles)
            {
                var profileElement = new XElement("profile",
                    new XAttribute("name", profile.Name));

                foreach (var path in profile.UncPaths)
                {
                    var normalizedDriveLetter = NormalizeDriveLetter(path.DriveLetter);
                    var pathElement = new XElement("uncPath",
                        new XAttribute("path", path.UncPath ?? string.Empty),
                        new XAttribute("username", path.Username ?? string.Empty),
                        new XAttribute("password", path.Password ?? string.Empty),
                        new XAttribute("driveLetter", normalizedDriveLetter));

                    profileElement.Add(pathElement);
                }

                root.Add(profileElement);
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                root
            );

            doc.Save(_profilesPath);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UncPathStore] Fehler beim Speichern der Profile: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Speichert ein einzelnes Profil (Umbenennungslogik)
    /// </summary>
    public bool SaveProfile(ObservableCollection<UncPathProfile> allProfiles, UncPathProfile profile, string? previousName = null)
    {
        try
        {
            // Profil nur hinzufügen, wenn es noch nicht in der Liste ist.
            // Dadurch bleiben Objekt-Identität und ListView-Selektion stabil.
            if (!allProfiles.Contains(profile))
                allProfiles.Add(profile);

            // Doppelte Namen verhindern (außer beim selben Objekt)
            var duplicate = allProfiles.FirstOrDefault(p =>
                !ReferenceEquals(p, profile) &&
                string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

            if (duplicate != null)
                return false;

            profile.IsDirty = false;
            return SaveProfiles(allProfiles);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UncPathStore] Fehler beim Speichern des Profils: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Löscht ein Profil
    /// </summary>
    public bool DeleteProfile(ObservableCollection<UncPathProfile> allProfiles, UncPathProfile profile)
    {
        try
        {
            allProfiles.Remove(profile);
            return SaveProfiles(allProfiles);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UncPathStore] Fehler beim Löschen des Profils: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Dupliziert ein Profil
    /// </summary>
    public UncPathProfile DuplicateProfile(UncPathProfile profile)
    {
        var newProfile = new UncPathProfile
        {
            Name = GetUniqueProfileName($"{profile.Name} (Kopie)", 1)
        };

        foreach (var path in profile.UncPaths)
        {
            newProfile.UncPaths.Add(new UncPathEntry
            {
                UncPath = path.UncPath,
                Username = path.Username,
                Password = path.Password,
                DriveLetter = path.DriveLetter
            });
        }

        return newProfile;
    }

    /// <summary>
    /// Generiert einen eindeutigen Profilnamen
    /// </summary>
    private string GetUniqueProfileName(string baseName, int index)
    {
        return baseName; // Später erweitern falls nötig
    }

    private static string NormalizeDriveLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim().TrimEnd(':');
        if (trimmed.Length != 1 || !char.IsLetter(trimmed[0]))
            return string.Empty;

        return char.ToUpperInvariant(trimmed[0]) + ":";
    }
}
