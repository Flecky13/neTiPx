using System.Diagnostics;
using System.Text.RegularExpressions;
using neTiPx.Core.Models;

namespace neTiPx.UI.Avalonia.Services;

/// <summary>
/// Service zum Anwenden von UNC-Pfad-Profilen (Cross-Platform: Windows, Linux, macOS)
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
    /// Mounted einen einzelnen UNC-Pfad (platform-spezifisch)
    /// </summary>
    private async Task<(bool Success, string Message)> MountUncPath(UncPathEntry entry)
    {
        if (OperatingSystem.IsWindows())
            return await MountUncPathWindows(entry);
        else if (OperatingSystem.IsLinux())
            return await MountUncPathLinux(entry);
        else if (OperatingSystem.IsMacOS())
            return await MountUncPathMacOS(entry);
        else
            return (false, "❌ Plattform nicht unterstützt");
    }

    #region Windows Mount/Unmount

    /// <summary>
    /// Windows: Mounted UNC-Pfad über net use
    /// </summary>
    private async Task<(bool Success, string Message)> MountUncPathWindows(UncPathEntry entry)
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = BuildWindowsNetUseCommand(entry),
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

    private string BuildWindowsNetUseCommand(UncPathEntry entry)
    {
        var uncPath = entry.ToWindowsPath().Trim();
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

    #endregion

    #region Linux Mount/Unmount

    /// <summary>
    /// Linux: Mounted CIFS/SMB Share über mount.cifs
    /// </summary>
    private async Task<(bool Success, string Message)> MountUncPathLinux(UncPathEntry entry)
    {
        return await Task.Run(() =>
        {
            try
            {
                var mountPoint = entry.MountPoint?.Trim();
                if (string.IsNullOrWhiteSpace(mountPoint))
                    return (false, $"❌ {entry.UncPath}: Kein Mount-Point angegeben");

                // Stelle sicher, dass das Mount-Point-Verzeichnis existiert
                if (!Directory.Exists(mountPoint))
                {
                    try
                    {
                        Directory.CreateDirectory(mountPoint);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"❌ {entry.UncPath}: Mount-Point konnte nicht erstellt werden: {ex.Message}");
                    }
                }

                var uncPath = entry.ToUnixPath().Trim();
                var command = BuildLinuxMountCommand(entry, uncPath, mountPoint);

                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return (false, $"❌ {entry.UncPath}: Prozess konnte nicht gestartet werden");

                    process.WaitForExit(15000);

                    var error = process.StandardError.ReadToEnd();
                    var output = process.StandardOutput.ReadToEnd();

                    if (process.ExitCode == 0)
                        return (true, $"✓ {entry.UncPath} -> {mountPoint}: Erfolgreich gemountet");
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

    private string BuildLinuxMountCommand(UncPathEntry entry, string uncPath, string mountPoint)
    {
        var username = entry.Username?.Trim() ?? string.Empty;
        var password = entry.Password ?? string.Empty;

        // Basis-Befehl: sudo mount -t cifs //server/share /mnt/mountpoint
        var cmd = $"sudo mount -t cifs '{uncPath}' '{mountPoint}'";

        // Optionen hinzufügen
        var options = new List<string>();

        if (!string.IsNullOrWhiteSpace(username))
        {
            options.Add($"username={username}");
            
            if (!string.IsNullOrWhiteSpace(password))
                options.Add($"password={password}");
        }
        else
        {
            options.Add("guest");
        }

        // Zusätzliche Optionen für bessere Kompatibilität
        options.Add("uid=1000");
        options.Add("gid=1000");
        options.Add("file_mode=0755");
        options.Add("dir_mode=0755");

        if (options.Count > 0)
            cmd += $" -o {string.Join(",", options)}";

        return cmd;
    }

    #endregion

    #region macOS Mount/Unmount

    /// <summary>
    /// macOS: Mounted SMB Share über mount_smbfs
    /// </summary>
    private async Task<(bool Success, string Message)> MountUncPathMacOS(UncPathEntry entry)
    {
        return await Task.Run(() =>
        {
            try
            {
                var mountPoint = entry.MountPoint?.Trim();
                if (string.IsNullOrWhiteSpace(mountPoint))
                    mountPoint = $"/Volumes/{Path.GetFileName(entry.UncPath?.Trim() ?? "share")}";

                // Stelle sicher, dass das Mount-Point-Verzeichnis existiert
                if (!Directory.Exists(mountPoint))
                {
                    try
                    {
                        Directory.CreateDirectory(mountPoint);
                    }
                    catch (Exception ex)
                    {
                        return (false, $"❌ {entry.UncPath}: Mount-Point konnte nicht erstellt werden: {ex.Message}");
                    }
                }

                var command = BuildMacOSMountCommand(entry, mountPoint);

                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{command}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return (false, $"❌ {entry.UncPath}: Prozess konnte nicht gestartet werden");

                    process.WaitForExit(15000);

                    var error = process.StandardError.ReadToEnd();
                    var output = process.StandardOutput.ReadToEnd();

                    if (process.ExitCode == 0)
                        return (true, $"✓ {entry.UncPath} -> {mountPoint}: Erfolgreich gemountet");
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

    private string BuildMacOSMountCommand(UncPathEntry entry, string mountPoint)
    {
        var uncPath = entry.ToUnixPath().Trim();
        var username = entry.Username?.Trim() ?? "guest";
        var password = entry.Password ?? string.Empty;

        // Format: mount_smbfs //user:password@server/share /Volumes/mountpoint
        string smbUrl;
        if (!string.IsNullOrWhiteSpace(password))
            smbUrl = uncPath.Replace("//", $"//{username}:{password}@");
        else
            smbUrl = uncPath.Replace("//", $"//{username}@");

        return $"mount_smbfs '{smbUrl}' '{mountPoint}'";
    }

    #endregion

    #region Disconnect

    /// <summary>
    /// Trennt eine gemountete UNC-Verbindung (platform-spezifisch)
    /// </summary>
    public async Task<(bool Success, string Message)> DisconnectMappedConnection(string target)
    {
        if (OperatingSystem.IsWindows())
            return await DisconnectWindows(target);
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            return await DisconnectUnix(target);
        else
            return (false, "Plattform nicht unterstützt");
    }

    private async Task<(bool Success, string Message)> DisconnectWindows(string target)
    {
        return await Task.Run(() =>
        {
            try
            {
                var normalizedTarget = NormalizeDisconnectTarget(target);
                if (string.IsNullOrWhiteSpace(normalizedTarget))
                    return (false, "Kein gültiges Trenn-Ziel übergeben.");

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

    private async Task<(bool Success, string Message)> DisconnectUnix(string mountPoint)
    {
        return await Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"sudo umount '{mountPoint}'\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return (false, "Prozess konnte nicht gestartet werden");

                    process.WaitForExit(10000);

                    if (process.ExitCode == 0)
                        return (true, $"✓ {mountPoint}: Verbindung getrennt");
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

    #endregion

    #region Get Mounted Connections

    /// <summary>
    /// Listet aktuell gemountete Netzwerkfreigaben (platform-spezifisch)
    /// </summary>
    public async Task<List<MountedUncConnection>> GetMountedConnections()
    {
        if (OperatingSystem.IsWindows())
            return await GetMountedConnectionsWindows();
        else if (OperatingSystem.IsLinux())
            return await GetMountedConnectionsLinux();
        else if (OperatingSystem.IsMacOS())
            return await GetMountedConnectionsMacOS();
        else
            return new List<MountedUncConnection>();
    }

    private async Task<List<MountedUncConnection>> GetMountedConnectionsWindows()
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

    private async Task<List<MountedUncConnection>> GetMountedConnectionsLinux()
    {
        return await Task.Run(() =>
        {
            var connections = new List<MountedUncConnection>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"mount | grep cifs\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return connections;

                    var output = process.StandardOutput.ReadToEnd();

                    // Format: //server/share on /mnt/mountpoint type cifs (options)
                    foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var match = Regex.Match(line, @"^(//[^\s]+)\s+on\s+([^\s]+)");
                        if (match.Success)
                        {
                            connections.Add(new MountedUncConnection
                            {
                                DriveLetter = string.Empty,
                                UncPath = match.Groups[1].Value,
                                DisconnectTarget = match.Groups[2].Value // Mount-Point zum Unmounten
                            });
                        }
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

    private async Task<List<MountedUncConnection>> GetMountedConnectionsMacOS()
    {
        return await Task.Run(() =>
        {
            var connections = new List<MountedUncConnection>();

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"mount | grep smbfs\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return connections;

                    var output = process.StandardOutput.ReadToEnd();

                    // Format: //user@server/share on /Volumes/mountpoint (smbfs, nodev, nosuid, mounted by user)
                    foreach (var line in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var match = Regex.Match(line, @"^//[^@]*@?([^\s]+)\s+on\s+([^\s]+)");
                        if (match.Success)
                        {
                            connections.Add(new MountedUncConnection
                            {
                                DriveLetter = string.Empty,
                                UncPath = "//" + match.Groups[1].Value,
                                DisconnectTarget = match.Groups[2].Value // Mount-Point zum Unmounten
                            });
                        }
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

    #endregion

    #region Helper Methods

    private static string NormalizeDriveLetter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim().TrimEnd(':');
        if (trimmed.Length != 1 || !char.IsLetter(trimmed[0]))
            return string.Empty;

        return char.ToUpperInvariant(trimmed[0]) + ":";
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

    #endregion
}
