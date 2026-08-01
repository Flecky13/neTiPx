using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using neTiPx.Core.Helpers;
using neTiPx.UI.Avalonia.Models;

namespace neTiPx.UI.Avalonia.Services;

public sealed class PingMonitorStore
{
    private readonly string _filePath = ConfigFileHelper.GetPingTargetsXmlPath();

    public IReadOnlyList<PingMonitorItem> Load()
    {
        var items = new List<PingMonitorItem>();

        try
        {
            if (!File.Exists(_filePath))
            {
                return items;
            }

            var doc = XDocument.Load(_filePath);
            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "pingTargets", StringComparison.OrdinalIgnoreCase))
            {
                return items;
            }

            foreach (var node in root.Elements("target"))
            {
                var idRaw = (string?)node.Attribute("id");
                var target = ((string?)node.Attribute("value") ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                var id = Guid.TryParse(idRaw, out var parsedId) ? parsedId : Guid.NewGuid();
                var interval = ParseInt((string?)node.Attribute("intervalSeconds"), 5, 1, 3600);
                var background = ParseBool((string?)node.Attribute("runInBackground"), true);
                var isActive = ParseBool((string?)node.Attribute("isActive"), true);
                var order = ParseInt((string?)node.Attribute("order"), int.MaxValue, 0, int.MaxValue);

                items.Add(new PingMonitorItem
                {
                    Id = id,
                    Target = target,
                    IntervalSeconds = interval,
                    RunInBackground = background,
                    IsActive = isActive,
                    Order = order
                });
            }
        }
        catch
        {
            return new List<PingMonitorItem>();
        }

        return items
            .OrderBy(i => i.Order)
            .ThenBy(i => i.Target, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void Save(IEnumerable<PingMonitorItem> items)
    {
        var list = items
            .OrderBy(i => i.Order)
            .ThenBy(i => i.Target, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var root = new XElement("pingTargets",
            list.Select(item => new XElement("target",
                new XAttribute("id", item.Id),
                new XAttribute("value", item.Target ?? string.Empty),
                new XAttribute("intervalSeconds", Math.Max(1, item.IntervalSeconds)),
                new XAttribute("runInBackground", item.RunInBackground),
                new XAttribute("isActive", item.IsActive),
                new XAttribute("order", Math.Max(0, item.Order)))));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);

        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        doc.Save(_filePath);
    }

    private static int ParseInt(string? raw, int defaultValue, int min, int max)
    {
        if (!int.TryParse(raw, out var value))
        {
            return defaultValue;
        }

        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        return bool.TryParse(raw, out var value) ? value : defaultValue;
    }
}
