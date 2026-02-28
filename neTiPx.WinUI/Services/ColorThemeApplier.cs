using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using neTiPx.WinUI.Models;

namespace neTiPx.WinUI.Services
{
    public sealed class ColorThemeApplier
    {
        public void Apply(ColorTheme theme)
        {
            var appResources = Application.Current.Resources;

            if (appResources.ThemeDictionaries.TryGetValue("Light", out var lightObj) && lightObj is ResourceDictionary lightDict)
            {
                ApplyThemeToDictionary(lightDict, theme);
            }

            if (appResources.ThemeDictionaries.TryGetValue("Dark", out var darkObj) && darkObj is ResourceDictionary darkDict)
            {
                ApplyThemeToDictionary(darkDict, theme);
            }

            appResources["AppBackgroundBrush"] = new SolidColorBrush(ParseColor(theme.AppBackgroundColor));
            appResources["CardBackgroundBrush"] = new SolidColorBrush(ParseColor(theme.CardBackgroundColor));
            appResources["CardBorderBrush"] = new SolidColorBrush(ParseColor(theme.CardBorderColor));
            appResources["AppTextBrush"] = new SolidColorBrush(ParseColor(theme.AppTextColor));
            appResources["AppTextSecondaryBrush"] = new SolidColorBrush(ParseColor(theme.AppTextSecondaryColor));

            appResources["NavigationViewItemForeground"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForeground));
            appResources["NavigationViewItemForegroundPointerOver"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundPointerOver));
            appResources["NavigationViewItemForegroundSelected"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundSelected));

            if (App.MainWindow.Content is FrameworkElement root)
            {
                var background = ParseColor(theme.AppBackgroundColor);
                var targetTheme = IsLightColor(background) ? ElementTheme.Light : ElementTheme.Dark;
                var intermediateTheme = targetTheme == ElementTheme.Light ? ElementTheme.Dark : ElementTheme.Light;

                root.RequestedTheme = intermediateTheme;
                root.RequestedTheme = targetTheme;
            }
        }

        private static void ApplyThemeToDictionary(ResourceDictionary dictionary, ColorTheme theme)
        {
            dictionary["AppBackgroundColor"] = ParseColor(theme.AppBackgroundColor);
            dictionary["CardBackgroundColor"] = ParseColor(theme.CardBackgroundColor);
            dictionary["CardBorderColor"] = ParseColor(theme.CardBorderColor);
            dictionary["AppTextColor"] = ParseColor(theme.AppTextColor);
            dictionary["AppTextSecondaryColor"] = ParseColor(theme.AppTextSecondaryColor);

            dictionary["NavigationViewItemForeground"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForeground));
            dictionary["NavigationViewItemForegroundPointerOver"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundPointerOver));
            dictionary["NavigationViewItemForegroundSelected"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundSelected));
        }

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
