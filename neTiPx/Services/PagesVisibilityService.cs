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

        private static readonly string[] _defaultPages = new[]
        {
            // Main pages
            "Adapters",
            "IpConfig",
            "Tools",
            "Info",
            "Settings",
            // Tools sub-pages
            "Ping",
            "Wlan",
            "NetworkCalculator",
            "NetworkScanner"
        };

        private static readonly string[] _xmlManagedPages = new[]
        {
            // Main pages (Adapters intentionally excluded)
            "IpConfig",
            "Tools",
            "Info",
            "Settings",
            // Tools sub-pages
            "Ping",
            "Wlan",
            "NetworkCalculator",
            "NetworkScanner"
        };

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
            if (!File.Exists(_configPath))
            {
                CreateNewConfigFile();
            }
            else
            {
                EnsureMissingPagesExist();
            }
        }

        private void CreateNewConfigFile()
        {
            var root = new XElement("pagesVisibility");
            foreach (var pageName in _xmlManagedPages)
            {
                root.Add(new XElement("page", new XAttribute("name", pageName), new XAttribute("visible", "true")));
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            doc.Save(_configPath);
        }

        private void EnsureMissingPagesExist()
        {
            try
            {
                var doc = XDocument.Load(_configPath);
                var root = doc.Root;
                if (root == null)
                {
                    CreateNewConfigFile();
                    return;
                }

                var existingPages = new HashSet<string>(
                    root.Elements("page")
                        .Select(p => p.Attribute("name")?.Value)
                        .Where(name => !string.IsNullOrWhiteSpace(name)),
                    StringComparer.OrdinalIgnoreCase
                );

                bool hasChanges = false;
                foreach (var pageName in _xmlManagedPages)
                {
                    if (!existingPages.Contains(pageName))
                    {
                        root.Add(new XElement("page", new XAttribute("name", pageName), new XAttribute("visible", "true")));
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    doc.Save(_configPath);
                }
            }
            catch
            {
            }
        }

        public Dictionary<string, bool> ReadPagesVisibility()
        {
            var visibility = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var pageName in _defaultPages)
            {
                visibility[pageName] = true;
            }

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

                // Adapters page is always visible and cannot be hidden via XML.
                visibility["Adapters"] = true;
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
                if (string.Equals(pageName, "Adapters", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

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
