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
        private const string LastSelectedFilePathAttribute = "lastSelectedPath";

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

        public string ReadLastSelectedFile()
        {
            var path = ConfigFileHelper.GetLogViewerRecentFilesXmlPath();
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                {
                    return string.Empty;
                }

                var lastSelected = root.Attribute(LastSelectedFilePathAttribute)?.Value;
                if (!string.IsNullOrWhiteSpace(lastSelected))
                {
                    return lastSelected.Trim();
                }

                return root.Elements("file")
                    .Select(element => element.Attribute("path")?.Value)
                    .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                    ?.Trim() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public void RemoveRecentFile(string filePath)
        {
            try
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "LogViewerStore", $"Datei aus Recent-Liste entfernen: {filePath}");
                var path = ConfigFileHelper.GetLogViewerRecentFilesXmlPath();
                if (!File.Exists(path))
                {
                    return;
                }

                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                {
                    return;
                }

                var toRemove = root.Elements("file")
                    .Where(element => string.Equals(element.Attribute("path")?.Value, filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var element in toRemove)
                {
                    element.Remove();
                }

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

        public void ValidateAndRemoveNonExistentFiles(IEnumerable<string> recentFiles)
        {
            try
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "LogViewerStore", "Validierung nicht-existenter Recent-Dateien");
                var nonExistentPaths = recentFiles
                    .Where(path => !string.IsNullOrWhiteSpace(path) && !File.Exists(path.Trim()))
                    .ToList();

                foreach (var path in nonExistentPaths)
                {
                    RemoveRecentFile(path);
                }
            }
            catch
            {
            }
        }

        public void WriteRecentFiles(IEnumerable<string> recentFiles, string? lastSelectedFilePath = null)
        {
            try
            {
                LogHandler.LogSystemMessage(LogLevel.INFO, "LogViewerStore", $"WriteRecentFiles: {recentFiles.Count()} Eintr\u00e4ge (zuletzt: {lastSelectedFilePath ?? "<keine>"})");
                var sanitized = recentFiles
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(MaxRecentFiles)
                    .ToList();

                var selectedPath = string.IsNullOrWhiteSpace(lastSelectedFilePath)
                    ? sanitized.FirstOrDefault() ?? string.Empty
                    : lastSelectedFilePath.Trim();

                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    var existingIndex = sanitized.FindIndex(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));
                    if (existingIndex >= 0)
                    {
                        sanitized.RemoveAt(existingIndex);
                    }

                    sanitized.Insert(0, selectedPath);
                    if (sanitized.Count > MaxRecentFiles)
                    {
                        sanitized = sanitized.Take(MaxRecentFiles).ToList();
                    }
                }

                var root = new XElement("logViewerRecentFiles",
                    sanitized.Select(path => new XElement("file", new XAttribute("path", path))));

                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    root.SetAttributeValue(LastSelectedFilePathAttribute, selectedPath);
                }

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

