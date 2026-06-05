namespace neTiPx.UI.Avalonia.Services
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
}
