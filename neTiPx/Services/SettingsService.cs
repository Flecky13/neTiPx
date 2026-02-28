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
    }
}
