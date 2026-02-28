using System;
using System.IO;
using System.Xml.Linq;
using neTiPx.Helpers;
using neTiPx.Models;

namespace neTiPx.Services
{
    public sealed class UserSettingsStore
    {
        public class UserSettings
        {
            public ColorTheme? ColorTheme { get; set; }
            public bool HoverWindowEnabled { get; set; } = true;
            public int HoverWindowDelaySeconds { get; set; } = 1;
        }

        public UserSettings ReadUserSettings()
        {
            var path = ConfigFileHelper.GetUserSettingsXmlPath();
            if (File.Exists(path))
            {
                return ReadFromXml(path);
            }

            return new UserSettings();
        }

        public void WriteUserSettings(UserSettings settings)
        {
            var path = ConfigFileHelper.GetUserSettingsXmlPath();

            // Existierende Einstellungen lesen, um sie zu bewahren
            var existingSettings = new UserSettings();
            if (File.Exists(path))
            {
                try
                {
                    var doc = XDocument.Load(path);
                    var root = doc.Root;
                    if (root != null)
                    {
                        var colorThemeElement = root.Element("colorTheme");
                        if (colorThemeElement != null && !string.IsNullOrWhiteSpace((string?)colorThemeElement.Attribute("name")))
                        {
                            existingSettings.ColorTheme = new ColorTheme
                            {
                                Name = (string?)colorThemeElement.Attribute("name") ?? string.Empty,
                                AppBackgroundColor = (string?)colorThemeElement.Attribute("appBackgroundColor") ?? "#F3F3F3",
                                CardBackgroundColor = (string?)colorThemeElement.Attribute("cardBackgroundColor") ?? "#FFFFFF",
                                CardBorderColor = (string?)colorThemeElement.Attribute("cardBorderColor") ?? "#E6E6E6",
                                AppTextColor = (string?)colorThemeElement.Attribute("appTextColor") ?? "#1A1A1A",
                                AppTextSecondaryColor = (string?)colorThemeElement.Attribute("appTextSecondaryColor") ?? "#5A5A5A",
                                NavigationViewItemForeground = (string?)colorThemeElement.Attribute("navigationViewItemForeground") ?? "#1A1A1A",
                                NavigationViewItemForegroundPointerOver = (string?)colorThemeElement.Attribute("navigationViewItemForegroundPointerOver") ?? "#1A1A1A",
                                NavigationViewItemForegroundSelected = (string?)colorThemeElement.Attribute("navigationViewItemForegroundSelected") ?? "#1A1A1A"
                            };
                        }
                    }
                }
                catch
                {
                    // Fehler beim Lesen, verwende Defaults
                }
            }

            // ColorTheme: Verwende neue Einstellung oder behalte existierende
            var colorThemeElement2 = new XElement("colorTheme");
            if (settings.ColorTheme != null)
            {
                colorThemeElement2 = new XElement("colorTheme",
                    new XAttribute("name", settings.ColorTheme.Name ?? string.Empty),
                    new XAttribute("appBackgroundColor", settings.ColorTheme.AppBackgroundColor ?? string.Empty),
                    new XAttribute("cardBackgroundColor", settings.ColorTheme.CardBackgroundColor ?? string.Empty),
                    new XAttribute("cardBorderColor", settings.ColorTheme.CardBorderColor ?? string.Empty),
                    new XAttribute("appTextColor", settings.ColorTheme.AppTextColor ?? string.Empty),
                    new XAttribute("appTextSecondaryColor", settings.ColorTheme.AppTextSecondaryColor ?? string.Empty),
                    new XAttribute("navigationViewItemForeground", settings.ColorTheme.NavigationViewItemForeground ?? string.Empty),
                    new XAttribute("navigationViewItemForegroundPointerOver", settings.ColorTheme.NavigationViewItemForegroundPointerOver ?? string.Empty),
                    new XAttribute("navigationViewItemForegroundSelected", settings.ColorTheme.NavigationViewItemForegroundSelected ?? string.Empty)
                );
            }
            else if (existingSettings.ColorTheme != null)
            {
                colorThemeElement2 = new XElement("colorTheme",
                    new XAttribute("name", existingSettings.ColorTheme.Name ?? string.Empty),
                    new XAttribute("appBackgroundColor", existingSettings.ColorTheme.AppBackgroundColor ?? string.Empty),
                    new XAttribute("cardBackgroundColor", existingSettings.ColorTheme.CardBackgroundColor ?? string.Empty),
                    new XAttribute("cardBorderColor", existingSettings.ColorTheme.CardBorderColor ?? string.Empty),
                    new XAttribute("appTextColor", existingSettings.ColorTheme.AppTextColor ?? string.Empty),
                    new XAttribute("appTextSecondaryColor", existingSettings.ColorTheme.AppTextSecondaryColor ?? string.Empty),
                    new XAttribute("navigationViewItemForeground", existingSettings.ColorTheme.NavigationViewItemForeground ?? string.Empty),
                    new XAttribute("navigationViewItemForegroundPointerOver", existingSettings.ColorTheme.NavigationViewItemForegroundPointerOver ?? string.Empty),
                    new XAttribute("navigationViewItemForegroundSelected", existingSettings.ColorTheme.NavigationViewItemForegroundSelected ?? string.Empty)
                );
            }

            var hoverWindowElement = new XElement("hoverWindow",
                new XAttribute("enabled", settings.HoverWindowEnabled),
                new XAttribute("delaySeconds", settings.HoverWindowDelaySeconds)
            );

            var root2 = new XElement("userSettings", colorThemeElement2, hoverWindowElement);
            var doc2 = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root2);
            doc2.Save(path);
        }

        private static UserSettings ReadFromXml(string path)
        {
            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                {
                    return new UserSettings();
                }

                var settings = new UserSettings();
                var colorThemeElement = root.Element("colorTheme");

                if (colorThemeElement != null)
                {
                    var themeName = (string?)colorThemeElement.Attribute("name");
                    if (!string.IsNullOrWhiteSpace(themeName))
                    {
                        settings.ColorTheme = new ColorTheme
                        {
                            Name = themeName,
                            AppBackgroundColor = (string?)colorThemeElement.Attribute("appBackgroundColor") ?? "#F3F3F3",
                            CardBackgroundColor = (string?)colorThemeElement.Attribute("cardBackgroundColor") ?? "#FFFFFF",
                            CardBorderColor = (string?)colorThemeElement.Attribute("cardBorderColor") ?? "#E6E6E6",
                            AppTextColor = (string?)colorThemeElement.Attribute("appTextColor") ?? "#1A1A1A",
                            AppTextSecondaryColor = (string?)colorThemeElement.Attribute("appTextSecondaryColor") ?? "#5A5A5A",
                            NavigationViewItemForeground = (string?)colorThemeElement.Attribute("navigationViewItemForeground") ?? "#1A1A1A",
                            NavigationViewItemForegroundPointerOver = (string?)colorThemeElement.Attribute("navigationViewItemForegroundPointerOver") ?? "#1A1A1A",
                            NavigationViewItemForegroundSelected = (string?)colorThemeElement.Attribute("navigationViewItemForegroundSelected") ?? "#1A1A1A"
                        };
                    }
                }

                var hoverWindowElement = root.Element("hoverWindow");
                if (hoverWindowElement != null)
                {
                    if (bool.TryParse((string?)hoverWindowElement.Attribute("enabled"), out var enabled))
                    {
                        settings.HoverWindowEnabled = enabled;
                    }
                    if (int.TryParse((string?)hoverWindowElement.Attribute("delaySeconds"), out var delay))
                    {
                        settings.HoverWindowDelaySeconds = delay;
                    }
                }

                return settings;
            }
            catch
            {
                return new UserSettings();
            }
        }
    }
}
