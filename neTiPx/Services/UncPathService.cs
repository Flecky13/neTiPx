using System.Diagnostics;
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
    /// Trennt einen UNC-Pfad
    /// </summary>
    public async Task<(bool Success, string Message)> DisconnectUncPath(string uncPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c net use \"{uncPath}\" /delete /yes",
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
                        return (true, $"✓ {uncPath}: Verbindung getrennt");
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
    /// Listet aktuell gemountete Netzwerkfreigaben auf
    /// </summary>
    public async Task<List<string>> GetMountedShares()
    {
        return await Task.Run(() =>
        {
            var shares = new List<string>();

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
                        return shares;

                    var output = process.StandardOutput.ReadToEnd();

                    // Parse output: lines mit \\ sind mounted shares
                    foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.Contains("\\\\"))
                            shares.Add(line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UncPathService] Fehler beim Auflisten: {ex.Message}");
            }

            return shares;
        });
    }
}
