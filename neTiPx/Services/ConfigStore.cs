using System;
using System.Collections.Generic;
using System.IO;
using neTiPx.Helpers;

namespace neTiPx.Services
{
    public sealed class ConfigStore
    {
        public Dictionary<string, string> ReadAll()
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var path = ConfigFileHelper.GetConfigIniPath();

            if (!File.Exists(path))
            {
                LogHandler.Log(LogLevel.INFO, "ConfigStore", $"ReadAll: Konfiguration nicht vorhanden ({path})");
                return values;
            }

            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("="))
                    {
                        continue;
                    }

                    var parts = line.Split(new[] { '=' }, 2);
                    values[parts[0].Trim()] = parts[1].Trim();
                }

                LogHandler.Log(LogLevel.INFO, "ConfigStore", $"ReadAll: {values.Count} Einträge geladen");
                return values;
            }
            catch (Exception ex)
            {
                LogHandler.Log(LogLevel.ERROR, "ConfigStore", "ReadAll: Lesen fehlgeschlagen", ex);
                return values;
            }
        }

        public void WriteAll(Dictionary<string, string> values)
        {
            var path = ConfigFileHelper.GetConfigIniPath();
            var outLines = new List<string>();

            if (values.TryGetValue("Adapter1", out var a1))
            {
                outLines.Add("Adapter1 = " + a1);
            }
            if (values.TryGetValue("Adapter2", out var a2))
            {
                outLines.Add("Adapter2 = " + a2);
            }

            foreach (var kv in values)
            {
                if (kv.Key.Equals("Adapter1", StringComparison.OrdinalIgnoreCase) || kv.Key.Equals("Adapter2", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                outLines.Add(kv.Key + " = " + kv.Value);
            }

            try
            {
                File.WriteAllLines(path, outLines);
                LogHandler.Log(LogLevel.INFO, "ConfigStore", $"WriteAll: {outLines.Count} Zeilen geschrieben");
            }
            catch (Exception ex)
            {
                LogHandler.Log(LogLevel.ERROR, "ConfigStore", "WriteAll: Schreiben fehlgeschlagen", ex);
            }
        }
    }
}

