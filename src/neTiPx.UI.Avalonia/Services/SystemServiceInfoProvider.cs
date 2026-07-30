using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace neTiPx.UI.Avalonia.Services;

public sealed record SystemServiceInfo(string Name, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>Reads the services that are managed by the current operating system.</summary>
public static class SystemServiceInfoProvider
{
    public static async Task<IReadOnlyList<SystemServiceInfo>> GetServicesAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            var output = await RunAsync("sc.exe", "query state= all");
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase))
                .Select(line => line["SERVICE_NAME:".Length..].Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Select(name => new SystemServiceInfo(name, name))
                .ToList();
        }

        if (OperatingSystem.IsLinux())
        {
            var output = await RunAsync("systemctl", "list-units --type=service --all --no-legend --no-pager");
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                .Where(name => !string.IsNullOrWhiteSpace(name) && name.EndsWith(".service", StringComparison.OrdinalIgnoreCase))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Select(name => new SystemServiceInfo(name, name))
                .ToList();
        }

        return Array.Empty<SystemServiceInfo>();
    }

    public static async Task<IReadOnlyDictionary<string, string>> GetStatusesAsync(IEnumerable<string> serviceNames)
    {
        var names = serviceNames.Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            var output = OperatingSystem.IsWindows()
                ? await RunAsync("sc.exe", $"query \"{name}\"")
                : OperatingSystem.IsLinux()
                    ? await RunAsync("systemctl", $"is-active \"{name}\"")
                    : string.Empty;

            result[name] = OperatingSystem.IsWindows()
                ? (output.Contains("STATE", StringComparison.OrdinalIgnoreCase) && output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) ? "Running" : "Stopped")
                : OperatingSystem.IsLinux()
                    ? (output.Trim().Equals("active", StringComparison.OrdinalIgnoreCase) ? "Running" : "Stopped")
                    : "-";
        }

        return result;
    }

    private static async Task<string> RunAsync(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
            {
                return string.Empty;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}
