using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using neTiPx.Models;

namespace neTiPx.Services
{
    public sealed class ColorThemeApplier
    {
        public void Apply(ColorTheme theme)
        {
            var appResources = Application.Current.Resources;
            var routeBrushes = GetRouteBrushes(theme);

            if (appResources.ThemeDictionaries.TryGetValue("Light", out var lightObj) && lightObj is ResourceDictionary lightDict)
            {
                ApplyThemeToDictionary(lightDict, theme, routeBrushes);
            }

            if (appResources.ThemeDictionaries.TryGetValue("Dark", out var darkObj) && darkObj is ResourceDictionary darkDict)
            {
                ApplyThemeToDictionary(darkDict, theme, routeBrushes);
            }

            appResources["AppBackgroundBrush"] = new SolidColorBrush(ParseColor(theme.AppBackgroundColor));
            appResources["CardBackgroundBrush"] = new SolidColorBrush(ParseColor(theme.CardBackgroundColor));
            appResources["CardBorderBrush"] = new SolidColorBrush(ParseColor(theme.CardBorderColor));
            appResources["AppTextBrush"] = new SolidColorBrush(ParseColor(theme.AppTextColor));
            appResources["AppTextSecondaryBrush"] = new SolidColorBrush(ParseColor(theme.AppTextSecondaryColor));

            appResources["NavigationViewItemForeground"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForeground));
            appResources["NavigationViewItemForegroundPointerOver"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundPointerOver));
            appResources["NavigationViewItemForegroundSelected"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundSelected));
            appResources["RouteModeActiveBrush"] = new SolidColorBrush(routeBrushes.Active);
            appResources["RouteModeInactiveBrush"] = new SolidColorBrush(routeBrushes.Inactive);
            appResources["RouteModeCountBrush"] = new SolidColorBrush(routeBrushes.Count);
            appResources["RouteModeDisabledBrush"] = new SolidColorBrush(routeBrushes.Disabled);

            if (App.MainWindow.Content is FrameworkElement root)
            {
                var background = ParseColor(theme.AppBackgroundColor);
                var targetTheme = IsLightColor(background) ? ElementTheme.Light : ElementTheme.Dark;
                var intermediateTheme = targetTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;

                root.RequestedTheme = intermediateTheme;
                root.RequestedTheme = targetTheme;
            }
        }

        private static void ApplyThemeToDictionary(ResourceDictionary dictionary, ColorTheme theme, RouteBrushes routeBrushes)
        {
            dictionary["AppBackgroundColor"] = ParseColor(theme.AppBackgroundColor);
            dictionary["CardBackgroundColor"] = ParseColor(theme.CardBackgroundColor);
            dictionary["CardBorderColor"] = ParseColor(theme.CardBorderColor);
            dictionary["AppTextColor"] = ParseColor(theme.AppTextColor);
            dictionary["AppTextSecondaryColor"] = ParseColor(theme.AppTextSecondaryColor);

            dictionary["NavigationViewItemForeground"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForeground));
            dictionary["NavigationViewItemForegroundPointerOver"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundPointerOver));
            dictionary["NavigationViewItemForegroundSelected"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundSelected));

            // AccentButton-Text immer Weiß: AccentButton hat immer farbigen (blauen) Hintergrund
            var white = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            var whiteDisabled = new SolidColorBrush(Windows.UI.Color.FromArgb(128, 255, 255, 255));
            dictionary["AccentButtonForeground"] = white;
            dictionary["AccentButtonForegroundPointerOver"] = white;
            dictionary["AccentButtonForegroundPressed"] = white;
            dictionary["AccentButtonForegroundDisabled"] = whiteDisabled;

            dictionary["RouteModeActiveBrush"] = new SolidColorBrush(routeBrushes.Active);
            dictionary["RouteModeInactiveBrush"] = new SolidColorBrush(routeBrushes.Inactive);
            dictionary["RouteModeCountBrush"] = new SolidColorBrush(routeBrushes.Count);
            dictionary["RouteModeDisabledBrush"] = new SolidColorBrush(routeBrushes.Disabled);
        }

        private static RouteBrushes GetRouteBrushes(ColorTheme theme)
        {
            // Für helle Themes mit sehr zarten Akzenten explizit kräftige, gut lesbare Farben setzen.
            if (string.Equals(theme.Name, "Weiß", StringComparison.OrdinalIgnoreCase)
                || string.Equals(theme.Name, "Weiss", StringComparison.OrdinalIgnoreCase))
            {
                return new RouteBrushes(
                    Active: Windows.UI.Color.FromArgb(255, 15, 108, 189),
                    Inactive: Windows.UI.Color.FromArgb(255, 55, 65, 81),
                    Count: Windows.UI.Color.FromArgb(255, 17, 24, 39),
                    Disabled: Windows.UI.Color.FromArgb(255, 148, 163, 184));
            }

            if (string.Equals(theme.Name, "Prinzessin", StringComparison.OrdinalIgnoreCase))
            {
                return new RouteBrushes(
                    Active: Windows.UI.Color.FromArgb(255, 194, 24, 91),
                    Inactive: Windows.UI.Color.FromArgb(255, 122, 30, 77),
                    Count: Windows.UI.Color.FromArgb(255, 90, 20, 56),
                    Disabled: Windows.UI.Color.FromArgb(255, 166, 77, 121));
            }

            return new RouteBrushes(
                Active: ParseColor(theme.NavigationViewItemForegroundSelected),
                Inactive: ParseColor(theme.AppTextSecondaryColor),
                Count: ParseColor(theme.AppTextColor),
                Disabled: Windows.UI.Color.FromArgb(255, 145, 145, 145));
        }

        private readonly record struct RouteBrushes(
            Windows.UI.Color Active,
            Windows.UI.Color Inactive,
            Windows.UI.Color Count,
            Windows.UI.Color Disabled);

        private static bool IsLightColor(Windows.UI.Color color)
        {
            var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            return luminance >= 0.6;
        }

        private static Windows.UI.Color ParseColor(string hexColor)
        {
            string hex = hexColor.Replace("#", "");

            if (hex.Length == 8)
            {
                byte a = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte r = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                return Windows.UI.Color.FromArgb(a, r, g, b);
            }

            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return Windows.UI.Color.FromArgb(255, r, g, b);
            }

            return Windows.UI.Color.FromArgb(255, 255, 255, 255);
        }
    }
}
