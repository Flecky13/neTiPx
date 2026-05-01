namespace neTiPx.Models;

/// <summary>
/// Repräsentiert eine aktuell gemountete UNC-Verbindung im System.
/// </summary>
public sealed class MountedUncConnection
{
    public string DriveLetter { get; set; } = string.Empty;
    public string UncPath { get; set; } = string.Empty;
    public string DisconnectTarget { get; set; } = string.Empty;

    public string DisplayText => string.IsNullOrWhiteSpace(DriveLetter)
        ? UncPath
        : $"{DriveLetter}  {UncPath}";
}
