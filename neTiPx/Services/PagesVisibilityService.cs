using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace neTiPx.Services
{
    public sealed class PagesVisibilityService
    {
        private static readonly string _configPath = GetPagesVisibilityXmlPath();

        private static string GetPagesVisibilityXmlPath()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dir = Path.Combine(appData, "neTiPx");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                return Path.Combine(dir, "PagesVisibility.xml");
            }
            catch
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PagesVisibility.xml");
            }
        }

        public void EnsureConfigExists()
        {
            if (File.Exists(_configPath))
            {
                return;
            }

            var root = new XElement("pagesVisibility",
                // Main pages
                new XElement("page", new XAttribute("name", "Adapters"), new XAttribute("visible", "true")),
                new XElement("page", new XAttribute("name", "IpConfig"), new XAttribute("visible", "true")),
                new XElement("page", new XAttribute("name", "Tools"), new XAttribute("visible", "true")),
                new XElement("page", new XAttribute("name", "Info"), new XAttribute("visible", "true")),
                new XElement("page", new XAttribute("name", "Settings"), new XAttribute("visible", "true")),
                // Tools sub-pages
                new XElement("page", new XAttribute("name", "Ping"), new XAttribute("visible", "true")),
                new XElement("page", new XAttribute("name", "Wlan"), new XAttribute("visible", "true")),
                new XElement("page", new XAttribute("name", "NetworkCalculator"), new XAttribute("visible", "true")),
                new XElement("page", new XAttribute("name", "NetworkScanner"), new XAttribute("visible", "true"))
            );

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            doc.Save(_configPath);
        }

        public Dictionary<string, bool> ReadPagesVisibility()
        {
            var visibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                // Main pages
                { "Adapters", true },
                { "IpConfig", true },
                { "Tools", true },
                { "Info", true },
                { "Settings", true },
                // Tools sub-pages
                { "Ping", true },
                { "Wlan", true },
                { "NetworkCalculator", true },
                { "NetworkScanner", true }
            };

            try
            {
                if (!File.Exists(_configPath))
                {
                    EnsureConfigExists();
                }

                var doc = XDocument.Load(_configPath);
                var root = doc.Root;
                if (root == null)
                {
                    return visibility;
                }

                foreach (var pageElement in root.Elements("page"))
                {
                    var nameAttr = pageElement.Attribute("name")?.Value;
                    var visibleAttr = pageElement.Attribute("visible")?.Value;

                    if (!string.IsNullOrWhiteSpace(nameAttr) && bool.TryParse(visibleAttr, out var isVisible))
                    {
                        visibility[nameAttr] = isVisible;
                    }
                }
            }
            catch
            {
            }

            return visibility;
        }

        public void UpdatePageVisibility(string pageName, bool visible)
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    EnsureConfigExists();
                }

                var doc = XDocument.Load(_configPath);
                var root = doc.Root;
                if (root == null)
                {
                    return;
                }

                var pageElement = root.Elements("page")
                    .FirstOrDefault(p => p.Attribute("name")?.Value == pageName);

                if (pageElement != null)
                {
                    pageElement.SetAttributeValue("visible", visible.ToString().ToLower());
                    doc.Save(_configPath);
                }
            }
            catch
            {
            }
        }
    }
}
