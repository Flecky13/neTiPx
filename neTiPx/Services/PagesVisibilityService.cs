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
            "NetworkScanner",
            "Routes"
        };

        private static readonly string[] _xmlManagedPages = new[]
        {
            // Main pages (always-visible pages intentionally excluded)
            "IpConfig",
            "Tools",
            // Tools sub-pages
            "Ping",
            "Wlan",
            "NetworkCalculator",
            "NetworkScanner",
            "Routes"
        };

        private static readonly string[] _alwaysVisiblePages = new[]
        {
            "Adapters",
            "Info",
            "Settings"
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
                        .OfType<string>()
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

                // Remove legacy entries for always-visible pages.
                foreach (var pageElement in root.Elements("page").ToList())
                {
                    var pageName = pageElement.Attribute("name")?.Value;
                    if (string.IsNullOrWhiteSpace(pageName))
                    {
                        continue;
                    }

                    if (_alwaysVisiblePages.Any(p => string.Equals(p, pageName, StringComparison.OrdinalIgnoreCase)))
                    {
                        pageElement.Remove();
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

                // Always-visible pages cannot be hidden via XML.
                foreach (var pageName in _alwaysVisiblePages)
                {
                    visibility[pageName] = true;
                }
            }
            catch
            {
            }

            return visibility;
        }

        public Dictionary<string, bool> ReadXmlManagedEntries()
        {
            var entries = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var pageName in _xmlManagedPages)
            {
                entries[pageName] = true;
            }

            try
            {
                EnsureConfigExists();

                var doc = XDocument.Load(_configPath);
                var root = doc.Root;
                if (root == null)
                {
                    return entries;
                }

                foreach (var pageElement in root.Elements("page"))
                {
                    var name = pageElement.Attribute("name")?.Value;
                    var visible = pageElement.Attribute("visible")?.Value;

                    if (string.IsNullOrWhiteSpace(name)
                        || !_xmlManagedPages.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    if (bool.TryParse(visible, out var isVisible))
                    {
                        entries[name] = isVisible;
                    }
                }
            }
            catch
            {
            }

            return entries;
        }

        public void SaveXmlManagedEntries(Dictionary<string, bool> entries)
        {
            try
            {
                EnsureConfigExists();

                var doc = XDocument.Load(_configPath);
                var root = doc.Root;
                if (root == null)
                {
                    return;
                }

                foreach (var pageName in _xmlManagedPages)
                {
                    var pageElement = root.Elements("page")
                        .FirstOrDefault(p => string.Equals(p.Attribute("name")?.Value, pageName, StringComparison.OrdinalIgnoreCase));

                    var newValue = entries.TryGetValue(pageName, out var isVisible) ? isVisible : true;

                    if (pageElement == null)
                    {
                        root.Add(new XElement("page",
                            new XAttribute("name", pageName),
                            new XAttribute("visible", newValue.ToString().ToLowerInvariant())));
                    }
                    else
                    {
                        pageElement.SetAttributeValue("visible", newValue.ToString().ToLowerInvariant());
                    }
                }

                doc.Save(_configPath);
            }
            catch
            {
            }
        }
    }
}
