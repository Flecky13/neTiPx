using System;
using neTiPx.Helpers;
using neTiPx.Models;

namespace neTiPx.Services
{
    public sealed class SettingsService
    {
        private const string SectionName = "AppSettings";
        private const string ThemeKey = "Theme";
        private const string ColorSchemeKey = "ColorScheme";

        private readonly UserSettingsStore _userSettingsStore = new UserSettingsStore();

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
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.ColorTheme;
        }

        public void SetColorTheme(ColorTheme colorTheme)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.ColorTheme = colorTheme;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public string GetColorSchemeName()
        {
            var colorTheme = GetColorTheme();
            return colorTheme?.Name ?? "Blau";
        }

        public void SetColorSchemeName(string colorSchemeName)
        {
            // Diese Methode wird jetzt nicht mehr verwendet,
            // aber für Rückwärtskompatibilität erhalten wir sie
        }

        public bool GetHoverWindowEnabled()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.HoverWindowEnabled;
        }

        public void SetHoverWindowEnabled(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.HoverWindowEnabled = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public int GetHoverWindowDelaySeconds()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.HoverWindowDelaySeconds;
        }

        public void SetHoverWindowDelaySeconds(int delaySeconds)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.HoverWindowDelaySeconds = delaySeconds;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetCheckConnectionGateway()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.CheckConnectionGateway;
        }

        public void SetCheckConnectionGateway(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.CheckConnectionGateway = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetCheckConnectionDns1()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.CheckConnectionDns1;
        }

        public void SetCheckConnectionDns1(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.CheckConnectionDns1 = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetCheckConnectionDns2()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.CheckConnectionDns2;
        }

        public void SetCheckConnectionDns2(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.CheckConnectionDns2 = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public int GetPingThresholdFast()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.PingThresholdFast;
        }

        public void SetPingThresholdFast(int value)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.PingThresholdFast = value;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public int GetPingThresholdNormal()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.PingThresholdNormal;
        }

        public void SetPingThresholdNormal(int value)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.PingThresholdNormal = value;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetCloseToTrayOnClose()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.CloseToTrayOnClose;
        }

        public void SetCloseToTrayOnClose(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.CloseToTrayOnClose = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public string? GetLastCheckedLatestVersion()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.LastCheckedLatestVersion;
        }

        public DateTime? GetLastCheckedAtLocal()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.LastCheckedAt?.ToLocalTime();
        }

        public void SetLastUpdateCheck(string latestVersion, DateTime lastCheckedLocal)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.LastCheckedLatestVersion = latestVersion;
            settings.LastCheckedAt = lastCheckedLocal.Kind == DateTimeKind.Utc
                ? lastCheckedLocal
                : lastCheckedLocal.ToUniversalTime();
            _userSettingsStore.WriteUserSettings(settings);
        }

        public string GetPingLogFolderPath()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.PingLogFolderPath ?? string.Empty;
        }

        public void SetPingLogFolderPath(string folderPath)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.PingLogFolderPath = folderPath ?? string.Empty;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetPingBackgroundActive()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.PingBackgroundActive;
        }

        public void SetPingBackgroundActive(bool active)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.PingBackgroundActive = active;
            _userSettingsStore.WriteUserSettings(settings);
        }

        // Network Scanner Port Settings
        public bool GetScanPortHttp()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.ScanPortHttp;
        }

        public void SetScanPortHttp(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.ScanPortHttp = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetScanPortHttps()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.ScanPortHttps;
        }

        public void SetScanPortHttps(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.ScanPortHttps = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetScanPortFtp()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.ScanPortFtp;
        }

        public void SetScanPortFtp(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.ScanPortFtp = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetScanPortSsh()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.ScanPortSsh;
        }

        public void SetScanPortSsh(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.ScanPortSsh = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetScanPortSmb()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.ScanPortSmb;
        }

        public void SetScanPortSmb(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.ScanPortSmb = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public bool GetScanPortRdp()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.ScanPortRdp;
        }

        public void SetScanPortRdp(bool enabled)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.ScanPortRdp = enabled;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public int GetCustomPort1()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.CustomPort1;
        }

        public void SetCustomPort1(int port)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.CustomPort1 = port;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public int GetCustomPort2()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.CustomPort2;
        }

        public void SetCustomPort2(int port)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.CustomPort2 = port;
            _userSettingsStore.WriteUserSettings(settings);
        }

        public int GetCustomPort3()
        {
            var settings = _userSettingsStore.ReadUserSettings();
            return settings.CustomPort3;
        }

        public void SetCustomPort3(int port)
        {
            var settings = _userSettingsStore.ReadUserSettings();
            settings.CustomPort3 = port;
            _userSettingsStore.WriteUserSettings(settings);
        }

    }
}
