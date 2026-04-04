using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using neTiPx.Helpers;

namespace neTiPx.Services
{
    public sealed class LogViewerStore
    {
        private const int MaxRecentFiles = 10;

        public IReadOnlyList<string> ReadRecentFiles()
        {
            var path = ConfigFileHelper.GetLogViewerRecentFilesXmlPath();
            if (!File.Exists(path))
            {
                return Array.Empty<string>();
            }

            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                {
                    return Array.Empty<string>();
                }

                return root.Elements("file")
                    .Select(element => element.Attribute("path")?.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxRecentFiles)
                    .ToList()!;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public void WriteRecentFiles(IEnumerable<string> recentFiles)
        {
            try
            {
                var sanitized = recentFiles
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxRecentFiles)
                    .ToList();

                var root = new XElement("logViewerRecentFiles",
                    sanitized.Select(path => new XElement("file", new XAttribute("path", path))));

                var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
                var path = ConfigFileHelper.GetLogViewerRecentFilesXmlPath();
                var directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                doc.Save(path);
            }
            catch
            {
            }
        }
    }
}
