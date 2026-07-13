using System.Collections.ObjectModel;
using System.Xml.Linq;
using neTiPx.Core.Models;

namespace neTiPx.UI.Avalonia.Services;

/// <summary>
/// Verwaltet die Persistierung von Routen-Profilen in XML
/// </summary>
public sealed class RouteProfileStore
{
    private readonly string _profilesPath;

    public RouteProfileStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "neTiPx"
        );

        if (!Directory.Exists(appDataPath))
            Directory.CreateDirectory(appDataPath);

        _profilesPath = Path.Combine(appDataPath, "Route-Profile.xml");
    }

    /// <summary>
    /// Liest alle Routen-Profile aus der Konfigurationsdatei
    /// </summary>
    public ObservableCollection<RouteProfile> LoadProfiles()
    {
        var profiles = new ObservableCollection<RouteProfile>();

        try
        {
            if (!File.Exists(_profilesPath))
                return profiles;

            var doc = XDocument.Load(_profilesPath);
            var root = doc.Root;

            if (root?.Name.LocalName != "routeProfiles")
                return profiles;

            foreach (var profileElement in root.Elements("profile"))
            {
                var profile = new RouteProfile
                {
                    Name = profileElement.Attribute("name")?.Value ?? "Unnamed"
                };

                // Routen laden
                foreach (var routeElement in profileElement.Elements("route"))
                {
                    var route = new RouteEntry
                    {
                        Destination = routeElement.Attribute("destination")?.Value ?? string.Empty,
                        SubnetMask = routeElement.Attribute("subnetMask")?.Value ?? string.Empty,
                        Gateway = routeElement.Attribute("gateway")?.Value ?? string.Empty
                    };

                    if (int.TryParse(routeElement.Attribute("metric")?.Value, out var metric))
                    {
                        route.Metric = metric;
                    }

                    profile.Routes.Add(route);
                }

                profiles.Add(profile);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RouteProfileStore] Fehler beim Laden der Profile: {ex.Message}");
        }

        return profiles;
    }

    /// <summary>
    /// Speichert alle Profile in der XML-Datei
    /// </summary>
    public bool SaveProfiles(IEnumerable<RouteProfile> profiles)
    {
        try
        {
            var root = new XElement("routeProfiles");

            foreach (var profile in profiles)
            {
                var profileElement = new XElement("profile",
                    new XAttribute("name", profile.Name));

                foreach (var route in profile.Routes)
                {
                    var routeElement = new XElement("route",
                        new XAttribute("destination", route.Destination ?? string.Empty),
                        new XAttribute("subnetMask", route.SubnetMask ?? string.Empty),
                        new XAttribute("gateway", route.Gateway ?? string.Empty),
                        new XAttribute("metric", route.Metric.ToString()));

                    profileElement.Add(routeElement);
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
            System.Diagnostics.Debug.WriteLine($"[RouteProfileStore] Fehler beim Speichern der Profile: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Speichert ein einzelnes Profil
    /// </summary>
    public bool SaveProfile(ObservableCollection<RouteProfile> allProfiles, RouteProfile profile, string? previousName = null)
    {
        try
        {
            // Profil nur hinzufügen, wenn es noch nicht in der Liste ist
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
            System.Diagnostics.Debug.WriteLine($"[RouteProfileStore] Fehler beim Speichern des Profils: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Löscht ein Profil
    /// </summary>
    public bool DeleteProfile(ObservableCollection<RouteProfile> allProfiles, RouteProfile profile)
    {
        try
        {
            allProfiles.Remove(profile);
            return SaveProfiles(allProfiles);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RouteProfileStore] Fehler beim Löschen des Profils: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Dupliziert ein Profil
    /// </summary>
    public RouteProfile DuplicateProfile(RouteProfile profile, ObservableCollection<RouteProfile> existingProfiles)
    {
        var newProfile = new RouteProfile
        {
            Name = GetUniqueProfileName($"{profile.Name} (Kopie)", 1, existingProfiles)
        };

        foreach (var route in profile.Routes)
        {
            newProfile.Routes.Add(new RouteEntry
            {
                Destination = route.Destination,
                SubnetMask = route.SubnetMask,
                Gateway = route.Gateway,
                Metric = route.Metric
            });
        }

        return newProfile;
    }

    /// <summary>
    /// Generiert einen eindeutigen Profilnamen
    /// </summary>
    private string GetUniqueProfileName(string baseName, int suffix, ObservableCollection<RouteProfile> existingProfiles)
    {
        var candidateName = suffix == 1 ? baseName : $"{baseName} {suffix}";

        var exists = existingProfiles.Any(p =>
            string.Equals(p.Name, candidateName, StringComparison.OrdinalIgnoreCase));

        if (exists)
            return GetUniqueProfileName(baseName, suffix + 1, existingProfiles);

        return candidateName;
    }
}
