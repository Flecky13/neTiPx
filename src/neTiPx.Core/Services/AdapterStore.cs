using System;
using System.IO;
using System.Xml.Linq;
using neTiPx.Core.Helpers;

namespace neTiPx.Core.Services;

/// <summary>
/// Provides access to network adapter configuration.
/// Stores which adapters are selected as primary and secondary.
/// </summary>
public sealed class AdapterStore
{
    /// <summary>
    /// Settings for selected network adapters.
    /// </summary>
    public class AdapterSettings
    {
        /// <summary>
        /// Name of the primary network adapter.
        /// </summary>
        public string? PrimaryAdapter { get; set; }

        /// <summary>
        /// Name of the secondary network adapter.
        /// </summary>
        public string? SecondaryAdapter { get; set; }
    }

    /// <summary>
    /// Reads the selected adapters from configuration.
    /// Automatically migrates from legacy INI format if needed.
    /// </summary>
    public AdapterSettings ReadAdapters()
    {
        var path = ConfigFileHelper.GetAdaptersXmlPath();
        if (File.Exists(path))
        {
            return ReadFromXml(path);
        }

        // Try to migrate from legacy INI file
        var migratedSettings = ReadFromLegacyIni();
        if (!string.IsNullOrWhiteSpace(migratedSettings.PrimaryAdapter) ||
            !string.IsNullOrWhiteSpace(migratedSettings.SecondaryAdapter))
        {
            WriteAdapters(migratedSettings);
        }

        return migratedSettings;
    }

    /// <summary>
    /// Writes the selected adapters to configuration.
    /// </summary>
    public void WriteAdapters(AdapterSettings settings)
    {
        var path = ConfigFileHelper.GetAdaptersXmlPath();
        var root = new XElement("adapters",
            new XAttribute("primaryAdapter", settings.PrimaryAdapter ?? string.Empty),
            new XAttribute("secondaryAdapter", settings.SecondaryAdapter ?? string.Empty)
        );

        var doc = new XDocument(root);
        doc.Save(path);
    }

    private static AdapterSettings ReadFromXml(string path)
    {
        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null)
            {
                return new AdapterSettings();
            }

            var settings = new AdapterSettings
            {
                PrimaryAdapter = (string?)root.Attribute("primaryAdapter"),
                SecondaryAdapter = (string?)root.Attribute("secondaryAdapter")
            };

            return settings;
        }
        catch
        {
            return new AdapterSettings();
        }
    }

    private static AdapterSettings ReadFromLegacyIni()
    {
        var configStore = new ConfigStore();
        var values = configStore.ReadAll();

        var settings = new AdapterSettings();

        if (values.TryGetValue("Adapter1", out var a1) && !string.IsNullOrWhiteSpace(a1))
        {
            settings.PrimaryAdapter = a1;
        }

        if (values.TryGetValue("Adapter2", out var a2) && !string.IsNullOrWhiteSpace(a2))
        {
            settings.SecondaryAdapter = a2;
        }

        return settings;
    }
}
