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
            var colorThemeElement = new XElement("colorTheme");

            if (settings.ColorTheme != null)
            {
                colorThemeElement = new XElement("colorTheme",
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

            var root = new XElement("userSettings", colorThemeElement);
            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            doc.Save(path);
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

                return settings;
            }
            catch
            {
                return new UserSettings();
            }
        }
    }
}
