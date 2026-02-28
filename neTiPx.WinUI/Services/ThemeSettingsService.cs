using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using neTiPx.WinUI.Models;

namespace neTiPx.WinUI.Services
{
    public sealed class ThemeSettingsService
    {
        private readonly string _settingsPath;
        private readonly string _themeFile;
        private readonly XmlSerializer _serializer;

        public ThemeSettingsService()
        {
            _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "neTiPx");
            _themeFile = Path.Combine(_settingsPath, "ThemeSettings.xml");
            _serializer = new XmlSerializer(typeof(List<ColorTheme>));
        }

        public List<ColorTheme> LoadThemes()
        {
            var defaultThemes = GetDefaultThemes();

            try
            {
                if (File.Exists(_themeFile))
                {
                    List<ColorTheme> loaded;

                    using (var reader = new StreamReader(_themeFile))
                    {
                        loaded = (List<ColorTheme>?)_serializer.Deserialize(reader) ?? new List<ColorTheme>();

                        foreach (var defaultTheme in defaultThemes)
                        {
                            var existingIndex = loaded.FindIndex(t => string.Equals(t.Name, defaultTheme.Name, StringComparison.OrdinalIgnoreCase));
                            if (existingIndex >= 0)
                            {
                                loaded[existingIndex] = defaultTheme;
                            }
                            else
                            {
                                loaded.Add(defaultTheme);
                            }
                        }
                    }

                    SaveThemes(loaded);

                    return loaded;
                }
                return defaultThemes;
            }
            catch (Exception)
            {
                // Bei Fehler Default-Themes zurückgeben
            }

            return defaultThemes;
        }

        public void SaveThemes(List<ColorTheme> themes)
        {
            try
            {
                // Verzeichnis erstellen wenn nicht vorhanden
                if (!Directory.Exists(_settingsPath))
                {
                    Directory.CreateDirectory(_settingsPath);
                }

                using (var writer = new StreamWriter(_themeFile))
                {
                    _serializer.Serialize(writer, themes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Speichern der Themen: {ex.Message}");
            }
        }

        private static List<ColorTheme> GetDefaultThemes()
        {
            return new List<ColorTheme>
            {
                new ColorTheme
                {
                    Name = "Rot",
                    AppBackgroundColor = "#FF2A1A1E",
                    CardBackgroundColor = "#FF342126",
                    CardBorderColor = "#FF4A2D33",
                    AppTextColor = "#FFFFFFFF",
                    AppTextSecondaryColor = "#FFE2C8CC",
                    NavigationViewItemForeground = "#FFFF8A80",
                    NavigationViewItemForegroundPointerOver = "#FFFFAB91",
                    NavigationViewItemForegroundSelected = "#FFFF5252"
                },
                new ColorTheme
                {
                    Name = "Orange",
                    AppBackgroundColor = "#FF2B1E16",
                    CardBackgroundColor = "#FF36251C",
                    CardBorderColor = "#FF4A3326",
                    AppTextColor = "#FFFFFFFF",
                    AppTextSecondaryColor = "#FFE7D2C4",
                    NavigationViewItemForeground = "#FFFFB74D",
                    NavigationViewItemForegroundPointerOver = "#FFFFD180",
                    NavigationViewItemForegroundSelected = "#FFFF9800"
                },
                new ColorTheme
                {
                    Name = "Prinzessin",
                    AppBackgroundColor = "#FFFFE4F1",              // sehr helles Rosa
                    CardBackgroundColor = "#FFFFF0F7",             // fast weiß mit Rosastich
                    CardBorderColor = "#FFFFB6D9",                 // Candy Pink Border
                    AppTextColor = "#FF7A1E4D",                    // dunkles Himbeer-Rosa
                    AppTextSecondaryColor = "#FFA64D79",           // weicher Rosaton
                    NavigationViewItemForeground = "#FFEC407A",    // kräftiges Pink
                    NavigationViewItemForegroundPointerOver = "#FFFF5C9C",
                    NavigationViewItemForegroundSelected = "#FFE91E63"
                },
                new ColorTheme
                {
                    Name = "Gelb",
                    AppBackgroundColor = "#FF2C2616",
                    CardBackgroundColor = "#FF38301C",
                    CardBorderColor = "#FF4D4226",
                    AppTextColor = "#FFFFFFFF",
                    AppTextSecondaryColor = "#FFE8DEC4",
                    NavigationViewItemForeground = "#FFFFD54F",
                    NavigationViewItemForegroundPointerOver = "#FFFFE082",
                    NavigationViewItemForegroundSelected = "#FFFFC107"
                },
                new ColorTheme
                {
                    Name = "Blau",
                    AppBackgroundColor = "#FF1A2430",
                    CardBackgroundColor = "#FF223040",
                    CardBorderColor = "#FF30465A",
                    AppTextColor = "#FFFFFFFF",
                    AppTextSecondaryColor = "#FFD0DAE5",
                    NavigationViewItemForeground = "#FF64B5F6",
                    NavigationViewItemForegroundPointerOver = "#FF90CAF9",
                    NavigationViewItemForegroundSelected = "#FF42A5F5"
                },
                new ColorTheme
                {
                    Name = "Grün",
                    AppBackgroundColor = "#FF18261E",
                    CardBackgroundColor = "#FF203226",
                    CardBorderColor = "#FF2E4737",
                    AppTextColor = "#FFFFFFFF",
                    AppTextSecondaryColor = "#FFD0E0D6",
                    NavigationViewItemForeground = "#FF66BB6A",
                    NavigationViewItemForegroundPointerOver = "#FF81C784",
                    NavigationViewItemForegroundSelected = "#FF4CAF50"
                },
                new ColorTheme
                {
                    Name = "Braun",
                    AppBackgroundColor = "#FF2A221C",
                    CardBackgroundColor = "#FF342A23",
                    CardBorderColor = "#FF4A3A30",
                    AppTextColor = "#FFFFFFFF",
                    AppTextSecondaryColor = "#FFE2D8D2",
                    NavigationViewItemForeground = "#FFA1887F",
                    NavigationViewItemForegroundPointerOver = "#FFBCAAA4",
                    NavigationViewItemForegroundSelected = "#FF8D6E63"
                },
                new ColorTheme
                {
                    Name = "Grau",
                    AppBackgroundColor = "#FF121212",
                    CardBackgroundColor = "#FF1E1E1E",
                    CardBorderColor = "#FF2C2C2C",
                    AppTextColor = "#FFF3F3F3",
                    AppTextSecondaryColor = "#FFBDBDBD",
                    NavigationViewItemForeground = "#FFCFCFCF",
                    NavigationViewItemForegroundPointerOver = "#FFFFFFFF",
                    NavigationViewItemForegroundSelected = "#FFE0E0E0"
                },
                new ColorTheme
                {
                    Name = "Weiß",
                    AppBackgroundColor = "#FFF4F6F8",
                    CardBackgroundColor = "#FFFFFFFF",
                    CardBorderColor = "#FFDCE3EA",
                    AppTextColor = "#FF1B1F23",
                    AppTextSecondaryColor = "#FF6B7280",
                    NavigationViewItemForeground = "#FF1F2328",
                    NavigationViewItemForegroundPointerOver = "#FF111111",
                    NavigationViewItemForegroundSelected = "#FF1D4ED8"
                },
                new ColorTheme
                {
                    Name = "Schwarz",
                    AppBackgroundColor = "#FF0A0A0A",
                    CardBackgroundColor = "#FF121212",
                    CardBorderColor = "#FF232323",
                    AppTextColor = "#FFFFFFFF",
                    AppTextSecondaryColor = "#FFB8B8B8",
                    NavigationViewItemForeground = "#FFE5E7EB",
                    NavigationViewItemForegroundPointerOver = "#FFFFFFFF",
                    NavigationViewItemForegroundSelected = "#FFF9FAFB"
                }
            };
        }
    }
}
