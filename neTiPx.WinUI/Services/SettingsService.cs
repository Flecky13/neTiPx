using neTiPx.WinUI.Helpers;

namespace neTiPx.WinUI.Services
{
    public sealed class SettingsService
    {
        private const string SectionName = "AppSettings";
        private const string ThemeKey = "Theme";
        private const string ColorSchemeKey = "ColorScheme";

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

        public string GetColorSchemeName()
        {
            var path = ConfigFileHelper.GetConfigIniPath();
            return IniFile.Read(SectionName, ColorSchemeKey, "Blau", path);
        }

        public void SetColorSchemeName(string colorSchemeName)
        {
            var path = ConfigFileHelper.GetConfigIniPath();
            IniFile.Write(SectionName, ColorSchemeKey, colorSchemeName, path);
        }
    }
}
