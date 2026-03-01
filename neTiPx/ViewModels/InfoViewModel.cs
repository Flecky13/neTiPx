using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using neTiPx.Helpers;
using neTiPx.Models;

namespace neTiPx.ViewModels
{
    public sealed class InfoViewModel : ObservableObject
    {
        private const string ReleasesPageUrl = "https://github.com/Flecky13/neTiPx/releases";
        private const string LatestReleaseApiUrl = "https://api.github.com/repos/Flecky13/neTiPx/releases/latest";

        private static readonly HttpClient HttpClient = CreateHttpClient();

        private readonly RelayCommand _checkForUpdateCommand;
        private readonly RelayCommand _installUpdateCommand;

        private string _latestVersion = "Noch nicht geprüft";
        private string _updateStatus = "Noch nicht geprüft";
        private bool _isUpdateAvailable;
        private GatewayStatusKind _updateStatusKind = GatewayStatusKind.Unknown;
        private Uri _latestReleaseUrl = new Uri("https://github.com/Flecky13/neTiPx/releases/latest");

        public InfoViewModel()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = version == null ? "Unknown" : version.ToString(3);

            _checkForUpdateCommand = new RelayCommand(CheckForUpdate);
            _installUpdateCommand = new RelayCommand(InstallUpdate, CanInstallUpdate);

            CheckForUpdateCommand = _checkForUpdateCommand;
            InstallUpdateCommand = _installUpdateCommand;
            ShowChangelogCommand = new RelayCommand(ShowChangelog);

            ReleasesUrl = new Uri(ReleasesPageUrl);
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

        private async void CheckForUpdate()
        {
            UpdateStatus = "Prüfe auf Updates...";
            UpdateStatusKind = GatewayStatusKind.Warning;

            try
            {
                var latestRelease = await GetLatestReleaseAsync();
                if (latestRelease == null)
                {
                    IsUpdateAvailable = false;
                    UpdateStatus = "Keine Release-Information von GitHub verfügbar.";
                    UpdateStatusKind = GatewayStatusKind.Bad;
                    return;
                }

                LatestVersion = latestRelease.Version;
                LatestReleaseUrl = latestRelease.ReleaseUri;

                var currentVersion = ParseVersion(AppVersion);
                var latestVersion = ParseVersion(latestRelease.Version);

                if (latestVersion > currentVersion)
                {
                    IsUpdateAvailable = true;
                    UpdateStatus = "Update verfügbar.";
                    UpdateStatusKind = GatewayStatusKind.Good;
                }
                else
                {
                    IsUpdateAvailable = false;
                    UpdateStatus = "Bereits auf dem neuesten Stand.";
                    UpdateStatusKind = GatewayStatusKind.Good;
                }
            }
            catch (Exception ex)
            {
                IsUpdateAvailable = false;
                UpdateStatus = "Update-Prüfung fehlgeschlagen.";
                UpdateStatusKind = GatewayStatusKind.Bad;
                Debug.WriteLine($"Update check failed: {ex}");
            }
        }

        private bool CanInstallUpdate()
        {
            return IsUpdateAvailable;
        }

        private void InstallUpdate()
        {
            OpenUrl(LatestReleaseUrl);
        }

        private void ShowChangelog()
        {
            OpenUrl(ReleasesUrl);
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

            return new GitHubRelease(tagName, releaseUri);
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
            public GitHubRelease(string version, Uri releaseUri)
            {
                Version = version;
                ReleaseUri = releaseUri;
            }

            public string Version { get; }
            public Uri ReleaseUri { get; }
        }
    }
}
