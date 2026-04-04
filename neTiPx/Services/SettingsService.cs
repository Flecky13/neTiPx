using System;
using neTiPx.Helpers;
using neTiPx.Models;

namespace neTiPx.Services
{
    public sealed class SettingsService
    {
        private const string SectionName = "AppSettings";
        private const string ThemeKey = "Theme";

        private readonly UserSettingsStore _userSettingsStore = new UserSettingsStore();
        private UserSettingsStore.UserSettings? _cachedSettings;

        private UserSettingsStore.UserSettings LoadUserSettings(bool forceReload = false)
        {
            if (forceReload || _cachedSettings == null)
            {
                _cachedSettings = _userSettingsStore.ReadUserSettings();
            }

            return _cachedSettings;
        }

        private void UpdateUserSettings(Action<UserSettingsStore.UserSettings> update)
        {
            var settings = LoadUserSettings(forceReload: true);
            update(settings);
            _userSettingsStore.WriteUserSettings(settings);
            _cachedSettings = settings;
        }

        public UserSettingsStore.UserSettings GetUserSettings()
        {
            return LoadUserSettings();
        }

        public ThemeOption GetThemeOption()
        {
            var path = ConfigFileHelper.GetConfigIniPath();
            var value = IniFile.Read(SectionName, ThemeKey, "System", path);

            return value switch
            {
                "Light" => ThemeOption.Light,
                "Dark" => ThemeOption.Dark,
                _ => ThemeOption.System
            };
        }

        public void SetThemeOption(ThemeOption option)
        {
            var path = ConfigFileHelper.GetConfigIniPath();
            var value = option switch
            {
                ThemeOption.Light => "Light",
                ThemeOption.Dark => "Dark",
                _ => "System"
            };

            IniFile.Write(SectionName, ThemeKey, value, path);
        }

        public ColorTheme? GetColorTheme()
        {
            var settings = LoadUserSettings();
            return settings.ColorTheme;
        }

        public void SetColorTheme(ColorTheme colorTheme)
        {
            UpdateUserSettings(settings => settings.ColorTheme = colorTheme);
        }

        public string GetColorSchemeName()
        {
            var colorTheme = GetColorTheme();
            return colorTheme?.Name ?? "Blau";
        }

        public bool GetHoverWindowEnabled()
        {
            var settings = LoadUserSettings();
            return settings.HoverWindowEnabled;
        }

        public void SetHoverWindowEnabled(bool enabled)
        {
            UpdateUserSettings(settings => settings.HoverWindowEnabled = enabled);
        }

        public int GetHoverWindowDelaySeconds()
        {
            var settings = LoadUserSettings();
            return settings.HoverWindowDelaySeconds;
        }

        public void SetHoverWindowDelaySeconds(int delaySeconds)
        {
            UpdateUserSettings(settings => settings.HoverWindowDelaySeconds = delaySeconds);
        }

        public string GetHoverWindowVerticalAnchor()
        {
            var settings = LoadUserSettings();
            return NormalizeHoverWindowVerticalAnchor(settings.HoverWindowVerticalAnchor);
        }

        public void SetHoverWindowVerticalAnchor(string verticalAnchor)
        {
            UpdateUserSettings(settings => settings.HoverWindowVerticalAnchor = NormalizeHoverWindowVerticalAnchor(verticalAnchor));
        }

        public int GetHoverWindowRightOffsetPixels()
        {
            var settings = LoadUserSettings();
            return Math.Max(0, settings.HoverWindowRightOffsetPixels);
        }

        public void SetHoverWindowRightOffsetPixels(int pixels)
        {
            UpdateUserSettings(settings => settings.HoverWindowRightOffsetPixels = Math.Max(0, pixels));
        }

        public int GetHoverWindowVerticalOffsetPixels()
        {
            var settings = LoadUserSettings();
            return Math.Max(0, settings.HoverWindowVerticalOffsetPixels);
        }

        public void SetHoverWindowVerticalOffsetPixels(int pixels)
        {
            UpdateUserSettings(settings => settings.HoverWindowVerticalOffsetPixels = Math.Max(0, pixels));
        }

        private static string NormalizeHoverWindowVerticalAnchor(string? verticalAnchor)
        {
            return string.Equals(verticalAnchor, "Top", StringComparison.OrdinalIgnoreCase)
                ? "Top"
                : "Bottom";
        }

        public bool GetCheckConnectionGateway()
        {
            var settings = LoadUserSettings();
            return settings.CheckConnectionGateway;
        }

        public void SetCheckConnectionGateway(bool enabled)
        {
            UpdateUserSettings(settings => settings.CheckConnectionGateway = enabled);
        }

        public bool GetCheckConnectionDns1()
        {
            var settings = LoadUserSettings();
            return settings.CheckConnectionDns1;
        }

        public void SetCheckConnectionDns1(bool enabled)
        {
            UpdateUserSettings(settings => settings.CheckConnectionDns1 = enabled);
        }

        public bool GetCheckConnectionDns2()
        {
            var settings = LoadUserSettings();
            return settings.CheckConnectionDns2;
        }

        public void SetCheckConnectionDns2(bool enabled)
        {
            UpdateUserSettings(settings => settings.CheckConnectionDns2 = enabled);
        }

        public int GetPingThresholdFast()
        {
            var settings = LoadUserSettings();
            return settings.PingThresholdFast;
        }

        public void SetPingThresholdFast(int value)
        {
            UpdateUserSettings(settings => settings.PingThresholdFast = value);
        }

        public int GetPingThresholdNormal()
        {
            var settings = LoadUserSettings();
            return settings.PingThresholdNormal;
        }

        public void SetPingThresholdNormal(int value)
        {
            UpdateUserSettings(settings => settings.PingThresholdNormal = value);
        }

        public bool GetCloseToTrayOnClose()
        {
            var settings = LoadUserSettings();
            return settings.CloseToTrayOnClose;
        }

        public void SetCloseToTrayOnClose(bool enabled)
        {
            UpdateUserSettings(settings => settings.CloseToTrayOnClose = enabled);
        }

        public string? GetLastCheckedLatestVersion()
        {
            var settings = LoadUserSettings();
            return settings.LastCheckedLatestVersion;
        }

        public DateTime? GetLastCheckedAtLocal()
        {
            var settings = LoadUserSettings();
            return settings.LastCheckedAt?.ToLocalTime();
        }

        public void SetLastUpdateCheck(string latestVersion, DateTime lastCheckedLocal)
        {
            UpdateUserSettings(settings =>
            {
                settings.LastCheckedLatestVersion = latestVersion;
                settings.LastCheckedAt = lastCheckedLocal.Kind == DateTimeKind.Utc
                    ? lastCheckedLocal
                    : lastCheckedLocal.ToUniversalTime();
            });
        }

        public string GetPingLogFolderPath()
        {
            var settings = LoadUserSettings();
            return settings.PingLogFolderPath ?? string.Empty;
        }

        public void SetPingLogFolderPath(string folderPath)
        {
            UpdateUserSettings(settings => settings.PingLogFolderPath = folderPath ?? string.Empty);
        }

        public bool GetPingBackgroundActive()
        {
            var settings = LoadUserSettings();
            return settings.PingBackgroundActive;
        }

        public void SetPingBackgroundActive(bool active)
        {
            UpdateUserSettings(settings => settings.PingBackgroundActive = active);
        }

        // Network Scanner Port Settings
        public bool GetScanPortHttp()
        {
            var settings = LoadUserSettings();
            return settings.ScanPortHttp;
        }

        public void SetScanPortHttp(bool enabled)
        {
            UpdateUserSettings(settings => settings.ScanPortHttp = enabled);
        }

        public bool GetScanPortHttps()
        {
            var settings = LoadUserSettings();
            return settings.ScanPortHttps;
        }

        public void SetScanPortHttps(bool enabled)
        {
            UpdateUserSettings(settings => settings.ScanPortHttps = enabled);
        }

        public bool GetScanPortFtp()
        {
            var settings = LoadUserSettings();
            return settings.ScanPortFtp;
        }

        public void SetScanPortFtp(bool enabled)
        {
            UpdateUserSettings(settings => settings.ScanPortFtp = enabled);
        }

        public bool GetScanPortSsh()
        {
            var settings = LoadUserSettings();
            return settings.ScanPortSsh;
        }

        public void SetScanPortSsh(bool enabled)
        {
            UpdateUserSettings(settings => settings.ScanPortSsh = enabled);
        }

        public bool GetScanPortSmb()
        {
            var settings = LoadUserSettings();
            return settings.ScanPortSmb;
        }

        public void SetScanPortSmb(bool enabled)
        {
            UpdateUserSettings(settings => settings.ScanPortSmb = enabled);
        }

        public bool GetScanPortRdp()
        {
            var settings = LoadUserSettings();
            return settings.ScanPortRdp;
        }

        public void SetScanPortRdp(bool enabled)
        {
            UpdateUserSettings(settings => settings.ScanPortRdp = enabled);
        }

        public int GetCustomPort1()
        {
            var settings = LoadUserSettings();
            return settings.CustomPort1;
        }

        public void SetCustomPort1(int port)
        {
            UpdateUserSettings(settings => settings.CustomPort1 = port);
        }

        public int GetCustomPort2()
        {
            var settings = LoadUserSettings();
            return settings.CustomPort2;
        }

        public void SetCustomPort2(int port)
        {
            UpdateUserSettings(settings => settings.CustomPort2 = port);
        }

        public int GetCustomPort3()
        {
            var settings = LoadUserSettings();
            return settings.CustomPort3;
        }

        public void SetCustomPort3(int port)
        {
            UpdateUserSettings(settings => settings.CustomPort3 = port);
        }

        public int GetNetworkScanMaxHosts()
        {
            var settings = LoadUserSettings();
            return Math.Max(1, settings.NetworkScanMaxHosts);
        }

        public void SetNetworkScanMaxHosts(int maxHosts)
        {
            UpdateUserSettings(settings => settings.NetworkScanMaxHosts = Math.Max(1, maxHosts));
        }

        // Language Settings
        public string GetLanguageCode()
        {
            var settings = LoadUserSettings();
            return settings.LanguageCode ?? "System";
        }

        public void SetLanguageCode(string languageCode)
        {
            UpdateUserSettings(settings => settings.LanguageCode = languageCode ?? "System");
        }

    }
}
