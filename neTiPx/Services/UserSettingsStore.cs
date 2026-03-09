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
            public bool CheckConnectionGateway { get; set; } = true;
            public bool CheckConnectionDns1 { get; set; } = true;
            public bool CheckConnectionDns2 { get; set; } = true;
            public int PingThresholdFast { get; set; } = 20;
            public int PingThresholdNormal { get; set; } = 50;
            public bool CloseToTrayOnClose { get; set; } = true;
            public string? LastCheckedLatestVersion { get; set; }
            public DateTime? LastCheckedAt { get; set; }
            public string PingLogFolderPath { get; set; } = string.Empty;
            public bool PingBackgroundActive { get; set; } = false;

            // Network Scanner Port Settings
            public bool ScanPortHttp { get; set; } = true;
            public bool ScanPortHttps { get; set; } = true;
            public bool ScanPortFtp { get; set; } = false;
            public bool ScanPortSsh { get; set; } = false;
            public bool ScanPortSmb { get; set; } = true;
            public bool ScanPortRdp { get; set; } = true;
            public int CustomPort1 { get; set; } = 0;
            public int CustomPort2 { get; set; } = 0;
            public int CustomPort3 { get; set; } = 0;
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

            var connectionStatusElement = new XElement("connectionStatus",
                new XAttribute("checkGateway", settings.CheckConnectionGateway),
                new XAttribute("checkDns1", settings.CheckConnectionDns1),
                new XAttribute("checkDns2", settings.CheckConnectionDns2),
                new XAttribute("thresholdFast", settings.PingThresholdFast),
                new XAttribute("thresholdNormal", settings.PingThresholdNormal)
            );

            var appBehaviorElement = new XElement("appBehavior",
                new XAttribute("closeToTrayOnClose", settings.CloseToTrayOnClose)
            );

            var updateCheckElement = new XElement("updateCheck");
            if (!string.IsNullOrWhiteSpace(settings.LastCheckedLatestVersion))
            {
                updateCheckElement.SetAttributeValue("latestVersion", settings.LastCheckedLatestVersion);
            }
            if (settings.LastCheckedAt.HasValue)
            {
                updateCheckElement.SetAttributeValue("lastCheckedAt", settings.LastCheckedAt.Value.ToString("o"));
            }

            var pingLoggingElement = new XElement("pingLogging",
                new XAttribute("folderPath", settings.PingLogFolderPath ?? string.Empty),
                new XAttribute("backgroundActive", settings.PingBackgroundActive)
            );

            var networkScannerElement = new XElement("networkScanner",
                new XAttribute("scanPortHttp", settings.ScanPortHttp),
                new XAttribute("scanPortHttps", settings.ScanPortHttps),
                new XAttribute("scanPortFtp", settings.ScanPortFtp),
                new XAttribute("scanPortSsh", settings.ScanPortSsh),
                new XAttribute("scanPortSmb", settings.ScanPortSmb),
                new XAttribute("scanPortRdp", settings.ScanPortRdp),
                new XAttribute("customPort1", settings.CustomPort1),
                new XAttribute("customPort2", settings.CustomPort2),
                new XAttribute("customPort3", settings.CustomPort3)
            );

            var root2 = new XElement("userSettings", colorThemeElement2, hoverWindowElement, connectionStatusElement, appBehaviorElement, updateCheckElement, pingLoggingElement, networkScannerElement);
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

                var connectionStatusElement = root.Element("connectionStatus");
                if (connectionStatusElement != null)
                {
                    if (bool.TryParse((string?)connectionStatusElement.Attribute("checkGateway"), out var checkGw))
                    {
                        settings.CheckConnectionGateway = checkGw;
                    }
                    if (bool.TryParse((string?)connectionStatusElement.Attribute("checkDns1"), out var checkDns1))
                    {
                        settings.CheckConnectionDns1 = checkDns1;
                    }
                    if (bool.TryParse((string?)connectionStatusElement.Attribute("checkDns2"), out var checkDns2))
                    {
                        settings.CheckConnectionDns2 = checkDns2;
                    }
                    if (int.TryParse((string?)connectionStatusElement.Attribute("thresholdFast"), out var thresholdFast))
                    {
                        settings.PingThresholdFast = thresholdFast;
                    }
                    if (int.TryParse((string?)connectionStatusElement.Attribute("thresholdNormal"), out var thresholdNormal))
                    {
                        settings.PingThresholdNormal = thresholdNormal;
                    }
                }

                var appBehaviorElement = root.Element("appBehavior");
                if (appBehaviorElement != null)
                {
                    if (bool.TryParse((string?)appBehaviorElement.Attribute("closeToTrayOnClose"), out var closeToTrayOnClose))
                    {
                        settings.CloseToTrayOnClose = closeToTrayOnClose;
                    }
                }

                var updateCheckElement = root.Element("updateCheck");
                if (updateCheckElement != null)
                {
                    settings.LastCheckedLatestVersion = (string?)updateCheckElement.Attribute("latestVersion");

                    var lastCheckedAtRaw = (string?)updateCheckElement.Attribute("lastCheckedAt");
                    if (DateTime.TryParse(lastCheckedAtRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastCheckedAt))
                    {
                        settings.LastCheckedAt = lastCheckedAt;
                    }
                }

                var pingLoggingElement = root.Element("pingLogging");
                if (pingLoggingElement != null)
                {
                    settings.PingLogFolderPath = (string?)pingLoggingElement.Attribute("folderPath") ?? string.Empty;
                    if (bool.TryParse((string?)pingLoggingElement.Attribute("backgroundActive"), out var backgroundActive))
                    {
                        settings.PingBackgroundActive = backgroundActive;
                    }
                }

                var networkScannerElement = root.Element("networkScanner");
                if (networkScannerElement != null)
                {
                    if (bool.TryParse((string?)networkScannerElement.Attribute("scanPortHttp"), out var scanPortHttp))
                        settings.ScanPortHttp = scanPortHttp;
                    if (bool.TryParse((string?)networkScannerElement.Attribute("scanPortHttps"), out var scanPortHttps))
                        settings.ScanPortHttps = scanPortHttps;
                    if (bool.TryParse((string?)networkScannerElement.Attribute("scanPortFtp"), out var scanPortFtp))
                        settings.ScanPortFtp = scanPortFtp;
                    if (bool.TryParse((string?)networkScannerElement.Attribute("scanPortSsh"), out var scanPortSsh))
                        settings.ScanPortSsh = scanPortSsh;
                    if (bool.TryParse((string?)networkScannerElement.Attribute("scanPortSmb"), out var scanPortSmb))
                        settings.ScanPortSmb = scanPortSmb;
                    if (bool.TryParse((string?)networkScannerElement.Attribute("scanPortRdp"), out var scanPortRdp))
                        settings.ScanPortRdp = scanPortRdp;
                    if (int.TryParse((string?)networkScannerElement.Attribute("customPort1"), out var customPort1))
                        settings.CustomPort1 = customPort1;
                    if (int.TryParse((string?)networkScannerElement.Attribute("customPort2"), out var customPort2))
                        settings.CustomPort2 = customPort2;
                    if (int.TryParse((string?)networkScannerElement.Attribute("customPort3"), out var customPort3))
                        settings.CustomPort3 = customPort3;
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
