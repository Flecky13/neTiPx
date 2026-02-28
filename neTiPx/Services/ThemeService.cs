using Microsoft.UI.Xaml;

namespace neTiPx.Services
{
    public enum ThemeOption
    {
        System,
        Light,
        Dark,
        Custom
    }

    public sealed class ThemeOptionItem
    {
        public ThemeOptionItem(string displayName, ThemeOption value)
        {
            DisplayName = displayName;
            Value = value;
        }

        public string DisplayName { get; }
        public ThemeOption Value { get; }
    }

    public sealed class ThemeService
    {
        private readonly SettingsService _settings = new SettingsService();

        public ThemeOption CurrentTheme => _settings.GetThemeOption();

        public void ApplyTheme(FrameworkElement root)
        {
            ApplyThemeInternal(root, CurrentTheme);
        }

        public void SetThemeOption(ThemeOption option)
        {
            _settings.SetThemeOption(option);
            if (App.MainWindow.Content is FrameworkElement root)
            {
                ApplyThemeInternal(root, option);
            }
        }

        private static void ApplyThemeInternal(FrameworkElement root, ThemeOption option)
        {
            root.RequestedTheme = option switch
            {
                ThemeOption.Light => ElementTheme.Light,
                ThemeOption.Dark => ElementTheme.Dark,
                ThemeOption.Custom => ElementTheme.Light, // Custom nutzt erstmal Light als Basis
                _ => ElementTheme.Default
            };
        }
    }
}
