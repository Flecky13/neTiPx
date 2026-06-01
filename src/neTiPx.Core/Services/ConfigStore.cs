using System;
using System.Collections.Generic;
using System.IO;
using neTiPx.Core.Helpers;

namespace neTiPx.Core.Services;

/// <summary>
/// Provides access to legacy INI-style configuration file.
/// Used for migration from older versions of neTiPx.
/// </summary>
public sealed class ConfigStore
{
    /// <summary>
    /// Reads all key-value pairs from the config.ini file.
    /// </summary>
    public Dictionary<string, string> ReadAll()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = ConfigFileHelper.GetConfigIniPath();

        if (!File.Exists(path))
        {
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

            return values;
        }
        catch (Exception)
        {
            // Return empty dictionary on error
            return values;
        }
    }

    /// <summary>
    /// Writes all key-value pairs to the config.ini file.
    /// </summary>
    public void WriteAll(Dictionary<string, string> values)
    {
        var path = ConfigFileHelper.GetConfigIniPath();
        var outLines = new List<string>();

        // Write Adapter settings first
        if (values.TryGetValue("Adapter1", out var a1))
        {
            outLines.Add("Adapter1 = " + a1);
        }
        if (values.TryGetValue("Adapter2", out var a2))
        {
            outLines.Add("Adapter2 = " + a2);
        }

        // Write remaining settings
        foreach (var kv in values)
        {
            if (kv.Key.Equals("Adapter1", StringComparison.OrdinalIgnoreCase) || 
                kv.Key.Equals("Adapter2", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            outLines.Add(kv.Key + " = " + kv.Value);
        }

        try
        {
            File.WriteAllLines(path, outLines);
        }
        catch (Exception)
        {
            // Ignore write errors
        }
    }
}
