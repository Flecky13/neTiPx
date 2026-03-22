using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;

namespace neTiPx.ViewModels
{
    public sealed class InfoViewModel : ObservableObject
    {
        private static readonly LanguageManager Lm = LanguageManager.Instance;
        private const string ReleasesPageUrl = "https://github.com/Flecky13/neTiPx/releases";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/Flecky13/neTiPx/releases/latest";
        private const string ReadmeUrl = "https://github.com/Flecky13/neTiPx/blob/master/README.md";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        private readonly RelayCommand _checkForUpdateCommand;
        private readonly RelayCommand _installUpdateCommand;
        private readonly SettingsService _settingsService = new SettingsService();

        private string _latestVersion = string.Empty;
        private string _latestVersionRaw = string.Empty;
        private string _updateStatus = string.Empty;
        private bool _isUpdateAvailable;
        private GatewayStatusKind _updateStatusKind = GatewayStatusKind.Unknown;
        private Uri _latestReleaseUrl = new Uri("https://github.com/Flecky13/neTiPx/releases/latest");
        private string? _setupDownloadUrl;

        public InfoViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = version == null ? T("INFO_UNKNOWN") : version.ToString(4);
            _latestVersion = T("INFO_STATUS_NOT_CHECKED");
            _updateStatus = T("INFO_STATUS_NOT_CHECKED");

            _checkForUpdateCommand = new RelayCommand(CheckForUpdate);
            _installUpdateCommand = new RelayCommand(InstallUpdate, CanInstallUpdate);

            CheckForUpdateCommand = _checkForUpdateCommand;
            InstallUpdateCommand = _installUpdateCommand;
            ShowChangelogCommand = new RelayCommand(ShowChangelog);
            ShowHelpCommand = new RelayCommand(ShowHelp);

            ReleasesUrl = new Uri(ReleasesPageUrl);

            _latestVersionRaw = _settingsService.GetLastCheckedLatestVersion() ?? string.Empty;
            var lastCheckedAt = _settingsService.GetLastCheckedAtLocal();
            if (!string.IsNullOrWhiteSpace(_latestVersionRaw))
            {
                LatestVersion = FormatLatestVersionDisplay(_latestVersionRaw, lastCheckedAt);
            }
        }

        public string AppName => "neTiPx";

        public string AppVersion { get; }
        public string LatestVersion
        {
            get => _latestVersion;
            private set => SetProperty(ref _latestVersion, value);
        }

        public string UpdateStatus
        {
            get => _updateStatus;
            private set => SetProperty(ref _updateStatus, value);
        }

        public Uri ReleasesUrl { get; }

        public Uri LatestReleaseUrl
        {
            get => _latestReleaseUrl;
            private set => SetProperty(ref _latestReleaseUrl, value);
        }

        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            private set
            {
                if (SetProperty(ref _isUpdateAvailable, value))
                {
                    _installUpdateCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public GatewayStatusKind UpdateStatusKind
        {
            get => _updateStatusKind;
            private set => SetProperty(ref _updateStatusKind, value);
        }

        public ICommand CheckForUpdateCommand { get; }
        public ICommand InstallUpdateCommand { get; }
        public ICommand ShowChangelogCommand { get; }
        public ICommand ShowHelpCommand { get; }

        private static string T(string key)
        {
            return Lm.Lang(key);
        }

        private async void CheckForUpdate()
        {
            UpdateStatus = T("INFO_STATUS_CHECKING");
            UpdateStatusKind = GatewayStatusKind.Warning;

            try
            {
                var latestRelease = await GetLatestReleaseAsync();
                if (latestRelease == null)
                {
                    IsUpdateAvailable = false;
                    UpdateStatus = T("INFO_STATUS_NO_RELEASE_INFO");
                    UpdateStatusKind = GatewayStatusKind.Bad;
                    return;
                }

                _latestVersionRaw = NormalizeVersionForDisplay(latestRelease.Version);
                var checkedAtLocal = DateTime.Now;

                LatestVersion = FormatLatestVersionDisplay(_latestVersionRaw, checkedAtLocal);
                LatestReleaseUrl = latestRelease.ReleaseUri;
                _setupDownloadUrl = latestRelease.SetupDownloadUrl;
                _settingsService.SetLastUpdateCheck(_latestVersionRaw, checkedAtLocal);

                var currentVersion = ParseVersion(AppVersion);
                var latestVersion = ParseVersion(latestRelease.Version);

                if (latestVersion > currentVersion)
                {
                    IsUpdateAvailable = true;
                    UpdateStatus = T("INFO_STATUS_UPDATE_AVAILABLE");
                    UpdateStatusKind = GatewayStatusKind.Good;
                }
                else
                {
                    IsUpdateAvailable = false;
                    UpdateStatus = T("INFO_STATUS_UP_TO_DATE");
                    UpdateStatusKind = GatewayStatusKind.Good;
                }
            }
            catch (Exception ex)
            {
                IsUpdateAvailable = false;
                UpdateStatus = T("INFO_STATUS_CHECK_FAILED");
                UpdateStatusKind = GatewayStatusKind.Bad;
                Debug.WriteLine($"Update check failed: {ex}");
            }
        }

        private bool CanInstallUpdate()
        {
            return IsUpdateAvailable;
        }

        private async void InstallUpdate()
        {
            if (string.IsNullOrWhiteSpace(_setupDownloadUrl))
            {
                // Fallback: Öffne Release-Seite
                OpenUrl(LatestReleaseUrl);
                return;
            }

            try
            {
                UpdateStatus = T("INFO_STATUS_DOWNLOADING");
                UpdateStatusKind = GatewayStatusKind.Warning;

                // Download Setup.exe
                var tempPath = Path.GetTempPath();
                var versionForFileName = _latestVersionRaw.Replace("V", string.Empty, StringComparison.OrdinalIgnoreCase);
                var setupFileName = $"neTiPx_Setup_{versionForFileName}.exe";
                var setupFilePath = Path.Combine(tempPath, setupFileName);

                using (var response = await HttpClient.GetAsync(_setupDownloadUrl))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fileStream = File.Create(setupFilePath))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }
                }

                UpdateStatus = T("INFO_STATUS_STARTING_INSTALL");

                // Starte Setup.exe
                Process.Start(new ProcessStartInfo
                {
                    FileName = setupFilePath,
                    UseShellExecute = true
                });

                // Beende die Anwendung
                await Task.Delay(500); // Kurze Verzögerung, damit Setup starten kann
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                UpdateStatus = T("INFO_STATUS_DOWNLOAD_FAILED_OPENING_RELEASE");
                UpdateStatusKind = GatewayStatusKind.Bad;
                Debug.WriteLine($"Failed to download/install update: {ex}");

                // Fallback: Öffne Release-Seite
                await Task.Delay(1500);
                OpenUrl(LatestReleaseUrl);
            }
        }

        private void ShowChangelog()
        {
            OpenUrl(ReleasesUrl);
        }

        private void ShowHelp()
        {
            OpenUrl(new Uri(ReadmeUrl));
        }

        private static async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            using var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(payload);

            var root = json.RootElement;
            var tagName = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString() ?? string.Empty
                : string.Empty;

            var htmlUrl = root.TryGetProperty("html_url", out var htmlElement)
                ? htmlElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(tagName))
            {
                return null;
            }

            var releaseUri = Uri.TryCreate(htmlUrl, UriKind.Absolute, out var parsedUri)
                ? parsedUri
                : new Uri("https://github.com/Flecky13/neTiPx/releases/latest");

            // Suche Setup.exe in Assets
            string? setupDownloadUrl = null;
            if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var nameElement))
                    {
                        var assetName = nameElement.GetString() ?? string.Empty;
                        if (assetName.StartsWith("neTiPx_Setup_", StringComparison.OrdinalIgnoreCase) &&
                            assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            if (asset.TryGetProperty("browser_download_url", out var downloadUrlElement))
                            {
                                setupDownloadUrl = downloadUrlElement.GetString();
                                break;
                            }
                        }
                    }
                }
            }

            return new GitHubRelease(tagName, releaseUri, setupDownloadUrl);
        }

        private static Version ParseVersion(string versionText)
        {
            var match = Regex.Match(versionText, @"\d+(?:\.\d+){0,3}");
            if (!match.Success)
            {
                return new Version(0, 0, 0, 0);
            }

            var rawVersion = match.Value;
            var parts = rawVersion.Split('.');
            if (parts.Length == 3)
            {
                rawVersion += ".0";
            }
            else if (parts.Length == 2)
            {
                rawVersion += ".0.0";
            }
            else if (parts.Length == 1)
            {
                rawVersion += ".0.0.0";
            }

            if (Version.TryParse(rawVersion, out var version))
            {
                return version;
            }

            return new Version(0, 0, 0, 0);
        }

        private static string NormalizeVersionForDisplay(string versionText)
        {
            var match = Regex.Match(versionText, @"\d+(?:\.\d+){0,3}");
            if (!match.Success)
            {
                return versionText;
            }

            var rawVersion = match.Value;
            var parts = rawVersion.Split('.');
            if (parts.Length == 3)
            {
                rawVersion += ".0";
            }
            else if (parts.Length == 2)
            {
                rawVersion += ".0.0";
            }
            else if (parts.Length == 1)
            {
                rawVersion += ".0.0.0";
            }

            return $"V{rawVersion}";
        }

        private static string FormatLatestVersionDisplay(string versionText, DateTime? checkedAtLocal)
        {
            if (!checkedAtLocal.HasValue)
            {
                return versionText;
            }

            return string.Format(CultureInfo.CurrentCulture, T("INFO_LAST_CHECKED_FORMAT"), versionText, checkedAtLocal.Value);
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("neTiPx/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        private static void OpenUrl(Uri uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to open URL '{uri}': {ex}");
            }
        }

        private sealed class GitHubRelease
        {
            public GitHubRelease(string version, Uri releaseUri, string? setupDownloadUrl)
            {
                Version = version;
                ReleaseUri = releaseUri;
                SetupDownloadUrl = setupDownloadUrl;
            }

            public string Version { get; }
            public Uri ReleaseUri { get; }
            public string? SetupDownloadUrl { get; }
        }
    }
}
