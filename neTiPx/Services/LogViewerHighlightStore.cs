using neTiPx.Helpers;
using neTiPx.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace neTiPx.Services
{
    public sealed class LogViewerHighlightStore
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public IReadOnlyList<LogViewerHighlightRule> ReadRules()
        {
            var path = ConfigFileHelper.GetLogViewerHighlightsJsonPath();
            if (!File.Exists(path))
            {
                return GetDefaultRules();
            }

            try
            {
                var json = File.ReadAllText(path);
                var payload = JsonSerializer.Deserialize<List<HighlightRuleDto>>(json, _jsonOptions) ?? new List<HighlightRuleDto>();
                var rules = payload
                    .Where(rule => !string.IsNullOrWhiteSpace(rule.SearchText))
                    .Select(rule => new LogViewerHighlightRule
                    {
                        SearchText = rule.SearchText?.Trim() ?? string.Empty,
                        ColorKey = string.IsNullOrWhiteSpace(rule.ColorKey) ? "red" : rule.ColorKey.Trim()
                    })
                    .ToList();

                return rules.Count > 0 ? rules : GetDefaultRules();
            }
            catch
            {
                return GetDefaultRules();
            }
        }

        public void WriteRules(IEnumerable<LogViewerHighlightRule> rules)
        {
            try
            {
                var path = ConfigFileHelper.GetLogViewerHighlightsJsonPath();
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var payload = rules
                    .Where(rule => !string.IsNullOrWhiteSpace(rule.SearchText))
                    .Select(rule => new HighlightRuleDto
                    {
                        SearchText = rule.SearchText.Trim(),
                        ColorKey = string.IsNullOrWhiteSpace(rule.ColorKey) ? "red" : rule.ColorKey
                    })
                    .ToList();

                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                File.WriteAllText(path, json);
            }
            catch
            {
            }
        }

        private static IReadOnlyList<LogViewerHighlightRule> GetDefaultRules()
        {
            return new[]
            {
                new LogViewerHighlightRule { SearchText = "ERROR", ColorKey = "red" },
                new LogViewerHighlightRule { SearchText = "WARN", ColorKey = "yellow" },
                new LogViewerHighlightRule { SearchText = "INFO", ColorKey = "green" }
            };
        }

        private sealed class HighlightRuleDto
        {
            public string SearchText { get; set; } = string.Empty;

            public string ColorKey { get; set; } = "red";
        }
    }
}
