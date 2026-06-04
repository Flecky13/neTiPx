using System;
using System.IO;
using System.Xml.Linq;

namespace neTiPx.Core.Services;

/// <summary>
/// Service to manage hover window settings (position and offsets).
/// </summary>
public sealed class HoverWindowSettings
{
    public class Settings
    {
        /// <summary>
        /// Vertical anchor: "Top" or "Bottom"
        /// </summary>
        public string VerticalAnchor { get; set; } = "Bottom";

        /// <summary>
        /// Offset from the right edge of the screen in pixels.
        /// </summary>
        public int RightOffsetPixels { get; set; } = 20;

        /// <summary>
        /// Offset from the vertical anchor (top or bottom) in pixels.
        /// </summary>
        public int VerticalOffsetPixels { get; set; } = 50;
    }

    /// <summary>
    /// Reads hover window settings from configuration.
    /// </summary>
    public Settings ReadSettings()
    {
        var path = GetSettingsFilePath();
        
        if (!File.Exists(path))
        {
            return new Settings();
        }

        try
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            
            if (root == null)
            {
                return new Settings();
            }

            var settings = new Settings
            {
                VerticalAnchor = (string?)root.Attribute("verticalAnchor") ?? "Bottom",
                RightOffsetPixels = int.TryParse((string?)root.Attribute("rightOffsetPixels"), out var right) ? right : 20,
                VerticalOffsetPixels = int.TryParse((string?)root.Attribute("verticalOffsetPixels"), out var vert) ? vert : 50
            };

            // Validate values
            settings.RightOffsetPixels = Math.Max(0, Math.Min(5000, settings.RightOffsetPixels));
            settings.VerticalOffsetPixels = Math.Max(0, Math.Min(5000, settings.VerticalOffsetPixels));

            if (settings.VerticalAnchor != "Top" && settings.VerticalAnchor != "Bottom")
            {
                settings.VerticalAnchor = "Bottom";
            }

            return settings;
        }
        catch
        {
            return new Settings();
        }
    }

    /// <summary>
    /// Writes hover window settings to configuration.
    /// </summary>
    public void WriteSettings(Settings settings)
    {
        var path = GetSettingsFilePath();
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var root = new XElement("hoverWindow",
            new XAttribute("verticalAnchor", settings.VerticalAnchor),
            new XAttribute("rightOffsetPixels", Math.Max(0, Math.Min(5000, settings.RightOffsetPixels))),
            new XAttribute("verticalOffsetPixels", Math.Max(0, Math.Min(5000, settings.VerticalOffsetPixels)))
        );

        var doc = new XDocument(root);
        doc.Save(path);
    }

    private static string GetSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configDir = Path.Combine(appData, "neTiPx");
        
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
        
        return Path.Combine(configDir, "hoverwindow.xml");
    }
}
