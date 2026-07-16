using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace neTiPx.Core.Services;

public class GitHubUpdateService
{
    private readonly HttpClient _httpClient;
    private const string RepoOwner = "Flecky13";
    private const string RepoName = "neTiPx";
    
    public GitHubUpdateService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "neTiPx-UpdateChecker");
    }

    public async Task<UpdateInfo> CheckForUpdatesAsync(Version currentVersion)
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url);
            
            if (release == null)
            {
                return new UpdateInfo 
                { 
                    IsUpdateAvailable = false, 
                    ErrorMessage = "Keine Release-Informationen gefunden" 
                };
            }

            // Version extrahieren (Format: v2.0.1.0 oder 2.0.1.0)
            var tagName = release.TagName.TrimStart('v');
            if (!Version.TryParse(tagName, out var latestVersion))
            {
                return new UpdateInfo 
                { 
                    IsUpdateAvailable = false, 
                    LatestVersion = release.TagName,
                    ErrorMessage = "Versions-Format nicht erkannt" 
                };
            }

            var isUpdateAvailable = latestVersion > currentVersion;
            
            // Plattform-spezifischen Download-Link finden
            var (downloadUrl, assetName) = GetPlatformSpecificAsset(release.Assets);
            
            return new UpdateInfo
            {
                IsUpdateAvailable = isUpdateAvailable,
                LatestVersion = latestVersion.ToString(),
                CurrentVersion = currentVersion.ToString(),
                DownloadUrl = downloadUrl,
                ReleaseUrl = release.HtmlUrl,
                ReleaseNotes = release.Body,
                PublishedAt = release.PublishedAt,
                AssetName = assetName
            };
        }
        catch (Exception ex)
        {
            return new UpdateInfo 
            { 
                IsUpdateAvailable = false, 
                ErrorMessage = $"Fehler bei der Prüfung: {ex.Message}" 
            };
        }
    }

    public async Task<string> DownloadAssetAsync(string downloadUrl, string? assetName = null)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new ArgumentException("Download URL must not be empty.", nameof(downloadUrl));
        }

        var fileName = ResolveAssetFileName(downloadUrl, assetName);
        var targetPath = Path.Combine(Path.GetTempPath(), "neTiPx", "updates", fileName);
        var targetDir = Path.GetDirectoryName(targetPath);

        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var destination = File.Create(targetPath);
        await source.CopyToAsync(destination);

        return targetPath;
    }

    private (string? downloadUrl, string? assetName) GetPlatformSpecificAsset(List<GitHubAsset> assets)
    {
        var platform = GetCurrentPlatform();
        var architecture = GetCurrentArchitecture();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var windowsAsset = GetPreferredWindowsAsset(assets, architecture);
            if (windowsAsset != null)
            {
                return (windowsAsset.BrowserDownloadUrl, windowsAsset.Name);
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var linuxAsset = GetPreferredLinuxAsset(assets, architecture);
            if (linuxAsset != null)
            {
                return (linuxAsset.BrowserDownloadUrl, linuxAsset.Name);
            }
        }
        
        // Suche nach passendem Asset basierend auf Plattform und Architektur
        var matchingAsset = assets.FirstOrDefault(a => 
            a.Name.Contains(platform, StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains(architecture, StringComparison.OrdinalIgnoreCase));
        
        if (matchingAsset != null)
        {
            return (matchingAsset.BrowserDownloadUrl, matchingAsset.Name);
        }
        
        // Fallback: Setup-Datei oder erste Asset
        var setupAsset = assets.FirstOrDefault(a => 
            a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        
        return (setupAsset?.BrowserDownloadUrl ?? assets.FirstOrDefault()?.BrowserDownloadUrl, 
                setupAsset?.Name ?? assets.FirstOrDefault()?.Name);
    }

    private static GitHubAsset? GetPreferredWindowsAsset(List<GitHubAsset> assets, string architecture)
    {
        var archTokens = GetArchitectureTokens(architecture);

        var candidates = assets
            .Where(a => a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase) ||
                        a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var setupExeWithArch = candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) &&
            archTokens.Any(t => a.Name.Contains(t, StringComparison.OrdinalIgnoreCase)));
        if (setupExeWithArch != null)
        {
            return setupExeWithArch;
        }

        var setupExeAny = candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("setup", StringComparison.OrdinalIgnoreCase));
        if (setupExeAny != null)
        {
            return setupExeAny;
        }

        var exeWithArch = candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            archTokens.Any(t => a.Name.Contains(t, StringComparison.OrdinalIgnoreCase)));
        if (exeWithArch != null)
        {
            return exeWithArch;
        }

        var exeAny = candidates.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (exeAny != null)
        {
            return exeAny;
        }

        return candidates.FirstOrDefault(a => a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
    }

    private static GitHubAsset? GetPreferredLinuxAsset(List<GitHubAsset> assets, string architecture)
    {
        var archTokens = GetArchitectureTokens(architecture);

        // Mint/Ubuntu/Debian: .deb zuerst, danach AppImage, dann tar/tar.gz.
        var candidates = assets
            .Where(a => a.Name.Contains("linux", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var debWithArch = candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase) &&
            archTokens.Any(t => a.Name.Contains(t, StringComparison.OrdinalIgnoreCase)));
        if (debWithArch != null)
        {
            return debWithArch;
        }

        var debAny = candidates.FirstOrDefault(a => a.Name.EndsWith(".deb", StringComparison.OrdinalIgnoreCase));
        if (debAny != null)
        {
            return debAny;
        }

        var appImage = candidates.FirstOrDefault(a => a.Name.EndsWith(".appimage", StringComparison.OrdinalIgnoreCase));
        if (appImage != null)
        {
            return appImage;
        }

        var tarWithArch = candidates.FirstOrDefault(a =>
            (a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || a.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)) &&
            archTokens.Any(t => a.Name.Contains(t, StringComparison.OrdinalIgnoreCase)));
        if (tarWithArch != null)
        {
            return tarWithArch;
        }

        return candidates.FirstOrDefault(a =>
            a.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> GetArchitectureTokens(string architecture)
    {
        return architecture.ToLowerInvariant() switch
        {
            "x64" => new List<string> { "x64", "amd64" },
            "x86" => new List<string> { "x86", "i386", "i686" },
            "arm64" => new List<string> { "arm64", "aarch64" },
            _ => new List<string> { architecture.ToLowerInvariant() }
        };
    }

    private string GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macos";
        
        return "unknown";
    }

    private string GetCurrentArchitecture()
    {
        var arch = RuntimeInformation.ProcessArchitecture;
        return arch switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "x64"
        };
    }

    public string GetPlatformDisplayName()
    {
        var platform = GetCurrentPlatform();
        var arch = GetCurrentArchitecture();
        return $"{platform}-{arch}";
    }

    private static string ResolveAssetFileName(string downloadUrl, string? assetName)
    {
        if (!string.IsNullOrWhiteSpace(assetName))
        {
            return assetName;
        }

        try
        {
            var uri = new Uri(downloadUrl);
            var fromUrl = Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath));
            if (!string.IsNullOrWhiteSpace(fromUrl))
            {
                return fromUrl;
            }
        }
        catch
        {
            // Ignore URL parsing errors and use fallback name.
        }

        return $"neTiPx-update-{DateTime.Now:yyyyMMdd-HHmmss}.bin";
    }
}

public class UpdateInfo
{
    public bool IsUpdateAvailable { get; set; }
    public string? LatestVersion { get; set; }
    public string? CurrentVersion { get; set; }
    public string? DownloadUrl { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? ReleaseNotes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? AssetName { get; set; }
    public string? ErrorMessage { get; set; }
}

// JSON-Modelle für GitHub API
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;
    
    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
    
    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }
    
    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

internal class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
}
