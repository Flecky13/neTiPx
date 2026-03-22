using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using neTiPx.Helpers;

namespace neTiPx.Services
{
    public sealed class PingTargetsStore
    {
        public sealed class PingTargetSettings
        {
            public string Target { get; set; } = string.Empty;

            public int IntervalSeconds { get; set; } = 5;

            public bool IsEnabled { get; set; } = true;

            public string Source { get; set; } = string.Empty;
        }

        public List<PingTargetSettings> ReadAll()
        {
            var path = ConfigFileHelper.GetPingTargetsXmlPath();
            if (!File.Exists(path))
            {
                return new List<PingTargetSettings>();
            }

            try
            {
                var doc = XDocument.Load(path);
                var root = doc.Root;
                if (root == null)
                {
                    return new List<PingTargetSettings>();
                }

                return root.Elements("target")
                    .Select(element => new PingTargetSettings
                    {
                        Target = (string?)element.Attribute("address") ?? string.Empty,
                        IntervalSeconds = ParseInterval((string?)element.Attribute("intervalSeconds")),
                        IsEnabled = ParseBool((string?)element.Attribute("enabled"), true),
                        Source = (string?)element.Attribute("source") ?? string.Empty
                    })
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Target))
                    .ToList();
            }
            catch
            {
                return new List<PingTargetSettings>();
            }
        }

        public void WriteAll(IEnumerable<PingTargetSettings> targets)
        {
            var safeTargets = targets
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Target))
                .Select(entry => new PingTargetSettings
                {
                    Target = entry.Target.Trim(),
                    IntervalSeconds = Math.Clamp(entry.IntervalSeconds, 1, 3600),
                    IsEnabled = entry.IsEnabled,
                    Source = entry.Source?.Trim() ?? string.Empty
                })
                .ToList();

            var root = new XElement("pingTargets",
                safeTargets.Select(entry =>
                {
                    var element = new XElement("target",
                        new XAttribute("address", entry.Target),
                        new XAttribute("intervalSeconds", entry.IntervalSeconds),
                        new XAttribute("enabled", entry.IsEnabled));

                    if (!string.IsNullOrWhiteSpace(entry.Source))
                    {
                        element.SetAttributeValue("source", entry.Source);
                    }

                    return element;
                }));

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
            var path = ConfigFileHelper.GetPingTargetsXmlPath();
            var directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            doc.Save(path);
        }

        private static int ParseInterval(string? raw)
        {
            return int.TryParse(raw, out var value) ? Math.Clamp(value, 1, 3600) : 5;
        }

        private static bool ParseBool(string? raw, bool defaultValue)
        {
            return bool.TryParse(raw, out var value) ? value : defaultValue;
        }
    }
}
