using System.Diagnostics;
using System.Text.RegularExpressions;
using neTiPx.Models;

namespace neTiPx.Services;

/// <summary>
/// Service zum Anwenden von UNC-Pfad-Profilen (net use Befehle)
/// </summary>
public sealed class UncPathService
{
    /// <summary>
    /// Wendet ein UNC-Pfad-Profil an (mapped alle UNC-Pfade)
    /// </summary>
    public async Task<(bool Success, string Message)> ApplyProfile(UncPathProfile profile)
    {
        if (profile == null || profile.UncPaths.Count == 0)
            return (false, "Keine UNC-Pfade im Profil vorhanden");

        var results = new List<string>();
        var hasErrors = false;

        foreach (var entry in profile.UncPaths)
        {
            if (string.IsNullOrWhiteSpace(entry.UncPath))
                continue;

            try
            {
                var (success, message) = await MountUncPath(entry);
                results.Add(message);

                if (!success)
                    hasErrors = true;
            }
            catch (Exception ex)
            {
                results.Add($"❌ {entry.UncPath}: {ex.Message}");
                hasErrors = true;
            }
        }

        var summaryMessage = string.Join("\n", results);
        return (!hasErrors, summaryMessage);
    }

    /// <summary>
    /// Mounted einen einzelnen UNC-Pfad über net use
    /// </summary>
    private async Task<(bool Success, string Message)> MountUncPath(UncPathEntry entry)
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = BuildNetUseCommand(entry),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return (false, $"❌ {entry.UncPath}: Prozess konnte nicht gestartet werden");

                    process.WaitForExit(10000); // 10 Sekunden Timeout

                    var error = process.StandardError.ReadToEnd();
                    var output = process.StandardOutput.ReadToEnd();

                    if (process.ExitCode == 0)
                        return (true, $"✓ {entry.UncPath}: Erfolgreich verbunden");
                    else
                        return (false, $"❌ {entry.UncPath}: {error ?? output}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"❌ {entry.UncPath}: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Baut den net use Befehl zusammen
    /// </summary>
    private string BuildNetUseCommand(UncPathEntry entry)
    {
        var uncPath = entry.UncPath?.Trim() ?? string.Empty;
        var driveLetter = NormalizeDriveLetter(entry.DriveLetter);
        var driveTarget = string.IsNullOrWhiteSpace(driveLetter) ? string.Empty : $"{driveLetter} ";

        // Ohne Authentifizierung
        if (string.IsNullOrWhiteSpace(entry.Username))
            return $"/c net use {driveTarget}\"{uncPath}\"";

        // Mit Authentifizierung
        var username = entry.Username?.Trim() ?? string.Empty;
        var password = entry.Password ?? string.Empty;

        // Passwort escapen falls nötig
        password = password.Replace("\"", "\"\"");

        return $"/c net use {driveTarget}\"{uncPath}\" \"{password}\" /user:{username}";
    }

    private static string NormalizeDriveLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim().TrimEnd(':');
        if (trimmed.Length != 1 || !char.IsLetter(trimmed[0]))
            return string.Empty;

        return char.ToUpperInvariant(trimmed[0]) + ":";
    }

    /// <summary>
    /// Trennt eine gemountete UNC-Verbindung anhand Laufwerksbuchstaben oder UNC-Pfad.
    /// </summary>
    public async Task<(bool Success, string Message)> DisconnectMappedConnection(string target)
    {
        return await Task.Run(() =>
        {
            try
            {
                var normalizedTarget = NormalizeDisconnectTarget(target);
                if (string.IsNullOrWhiteSpace(normalizedTarget))
                    return (false, "Kein gueltiges Trenn-Ziel uebergeben.");

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c net use \"{normalizedTarget}\" /delete /yes",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return (false, $"Prozess konnte nicht gestartet werden");

                    process.WaitForExit(10000);

                    if (process.ExitCode == 0)
                        return (true, $"✓ {normalizedTarget}: Verbindung getrennt");
                    else
                    {
                        var error = process.StandardError.ReadToEnd();
                        return (false, error ?? "Fehler beim Trennen der Verbindung");
                    }
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });
    }

    /// <summary>
    /// Listet aktuell gemountete Netzwerkfreigaben strukturiert auf.
    /// </summary>
    public async Task<List<MountedUncConnection>> GetMountedConnections()
    {
        return await Task.Run(() =>
        {
            var connections = new List<MountedUncConnection>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c net use",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return connections;

                    var output = process.StandardOutput.ReadToEnd();

                    // Parse output: Zeilen mit UNC-Pfaden (\\server\share)
                    foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!line.Contains("\\\\", StringComparison.Ordinal))
                            continue;

                        var tokens = Regex.Split(line.Trim(), "\\s+");
                        var remoteIndex = Array.FindIndex(tokens, t => t.StartsWith("\\\\", StringComparison.Ordinal));

                        if (remoteIndex < 0)
                            continue;

                        var remote = tokens[remoteIndex].Trim();
                        var drive = string.Empty;

                        if (remoteIndex > 0 && IsDriveToken(tokens[remoteIndex - 1]))
                            drive = NormalizeDriveLetter(tokens[remoteIndex - 1]);

                        if (string.IsNullOrWhiteSpace(remote))
                            continue;

                        connections.Add(new MountedUncConnection
                        {
                            DriveLetter = drive,
                            UncPath = remote,
                            DisconnectTarget = string.IsNullOrWhiteSpace(drive) ? remote : drive
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UncPathService] Fehler beim Auflisten: {ex.Message}");
            }

            return connections;
        });
    }

    private static bool IsDriveToken(string value)
    {
        return NormalizeDriveLetter(value).Length == 2;
    }

    private static string NormalizeDisconnectTarget(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var drive = NormalizeDriveLetter(value);
        if (!string.IsNullOrWhiteSpace(drive))
            return drive;

        var trimmed = value.Trim();
        return trimmed.StartsWith("\\\\", StringComparison.Ordinal) ? trimmed : string.Empty;
    }
}
