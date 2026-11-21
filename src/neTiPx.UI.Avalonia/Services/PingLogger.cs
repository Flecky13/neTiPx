using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using neTiPx.UI.Avalonia.Models;

namespace neTiPx.UI.Avalonia.Services;

/// <summary>
/// Logging-Service für Ping-Ergebnisse
/// Schreibt ein Log pro Zieladresse (zieladresse.log)
/// </summary>
public sealed class PingLogger : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
    private readonly object _lockSync = new();
    private bool _disposed;

    public PingLogger(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Protokolliert einen erfolgreichen Ping
    /// </summary>
    public async Task LogPingSuccessAsync(string target, AddressFamily addressFamily, long latencyMs)
    {
        try
        {
            if (!_settingsService.GetPingLoggingEnabled())
            {
                return;
            }

            var logDirectory = _settingsService.GetPingLogDirectory();
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var ipVersion = addressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
            var message = $"[{timestamp}] SUCCESS {ipVersion} - Latency: {latencyMs}ms";

            await LogToFileAsync(target, logDirectory, message);
        }
        catch
        {
            // Logging-Fehler sollten die Hauptfunktionalität nicht beeinflussen
        }
    }

    /// <summary>
    /// Protokolliert einen fehlgeschlagenen Ping
    /// </summary>
    public async Task LogPingFailureAsync(string target, AddressFamily addressFamily, string errorMessage)
    {
        try
        {
            if (!_settingsService.GetPingLoggingEnabled())
            {
                return;
            }

            var logDirectory = _settingsService.GetPingLogDirectory();
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var ipVersion = addressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
            var message = $"[{timestamp}] FAILURE {ipVersion} - {errorMessage}";

            await LogToFileAsync(target, logDirectory, message);
        }
        catch
        {
            // Logging-Fehler sollten die Hauptfunktionalität nicht beeinflussen
        }
    }

    /// <summary>
    /// Protokolliert einen DNS-Fehler
    /// </summary>
    public async Task LogDnsErrorAsync(string target, string errorMessage)
    {
        try
        {
            if (!_settingsService.GetPingLoggingEnabled())
            {
                return;
            }

            var logDirectory = _settingsService.GetPingLogDirectory();
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var message = $"[{timestamp}] DNS_ERROR - {errorMessage}";

            await LogToFileAsync(target, logDirectory, message);
        }
        catch
        {
            // Logging-Fehler sollten die Hauptfunktionalität nicht beeinflussen
        }
    }

    private async Task LogToFileAsync(string target, string logDirectory, string message)
    {
        // Zieladresse bereinigen für sicheren Dateinamen
        var safeTarget = SanitizeFilename(target);
        var logFileName = $"{safeTarget}.log";
        var logFilePath = Path.Combine(logDirectory, logFileName);

        // Lock pro Datei, um Thread-Sicherheit zu gewährleisten
        SemaphoreSlim fileLock;
        lock (_lockSync)
        {
            if (!_fileLocks.TryGetValue(logFilePath, out fileLock!))
            {
                fileLock = new SemaphoreSlim(1, 1);
                _fileLocks[logFilePath] = fileLock;
            }
        }

        await fileLock.WaitAsync();
        try
        {
            // Directory erstellen falls nicht vorhanden
            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Nachricht in die Log-Datei schreiben
            await File.AppendAllTextAsync(logFilePath, message + Environment.NewLine, Encoding.UTF8);
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <summary>
    /// Bereitet den Ziel-Namen für einen sicheren Dateinamen auf
    /// </summary>
    private static string SanitizeFilename(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return "unknown";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder();

        foreach (var c in target)
        {
            if (Array.IndexOf(invalidChars, c) >= 0)
            {
                sb.Append('_');
            }
            else
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString();
        // Länge begrenzen (Windows: max 255 Zeichen für Dateinalte)
        return result.Length > 200 ? result.Substring(0, 200) : result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_lockSync)
        {
            foreach (var lockItem in _fileLocks.Values)
            {
                lockItem.Dispose();
            }
            _fileLocks.Clear();
        }
    }
}
