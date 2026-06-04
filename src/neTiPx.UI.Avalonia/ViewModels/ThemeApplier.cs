using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using neTiPx.Core.Models;

namespace neTiPx.UI.Avalonia.ViewModels;

/// <summary>
/// Applies color themes to Avalonia application resources.
/// </summary>
public static class ThemeApplier
{
    /// <summary>
    /// Applies a color theme to the application.
    /// </summary>
    public static void Apply(ColorTheme theme)
    {
        if (Application.Current == null)
        {
            return;
        }

        var resources = Application.Current.Resources;

        // Parse and apply colors
        var appBgColor = ParseColor(theme.AppBackgroundColor);
        var cardBgColor = ParseColor(theme.CardBackgroundColor);
        var cardBorderColor = ParseColor(theme.CardBorderColor);
        var textColor = ParseColor(theme.AppTextColor);
        var textSecondaryColor = ParseColor(theme.AppTextSecondaryColor);
        
        resources["AppBackgroundColor"] = appBgColor;
        resources["CardBackgroundColor"] = cardBgColor;
        resources["CardBorderColor"] = cardBorderColor;
        resources["AppTextColor"] = textColor;
        resources["AppTextSecondaryColor"] = textSecondaryColor;

        // Create brushes
        resources["AppBackgroundBrush"] = new SolidColorBrush(appBgColor);
        resources["CardBackgroundBrush"] = new SolidColorBrush(cardBgColor);
        resources["CardBorderBrush"] = new SolidColorBrush(cardBorderColor);
        resources["AppTextBrush"] = new SolidColorBrush(textColor);
        resources["AppTextSecondaryBrush"] = new SolidColorBrush(textSecondaryColor);

        resources["NavigationViewItemForeground"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForeground));
        resources["NavigationViewItemForegroundPointerOver"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundPointerOver));
        resources["NavigationViewItemForegroundSelected"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundSelected));
        
        // Override Avalonia system resources to apply theme globally
        resources["SystemControlBackgroundAltHighBrush"] = new SolidColorBrush(cardBgColor);
        resources["SystemControlBackgroundAltMediumBrush"] = new SolidColorBrush(appBgColor);
        resources["SystemControlForegroundBaseLowBrush"] = new SolidColorBrush(cardBorderColor);
        resources["SystemControlForegroundBaseHighBrush"] = new SolidColorBrush(textColor);
        resources["SystemControlForegroundBaseMediumBrush"] = new SolidColorBrush(textSecondaryColor);
        resources["SystemAccentColor"] = ParseColor(theme.NavigationViewItemForegroundSelected);
        
        // ComboBox and Popup resources
        resources["ComboBoxBackground"] = new SolidColorBrush(cardBgColor);
        resources["ComboBoxForeground"] = new SolidColorBrush(textColor);
        resources["ComboBoxBorderBrush"] = new SolidColorBrush(cardBorderColor);
        resources["ComboBoxDropDownBackground"] = new SolidColorBrush(cardBgColor);
        resources["ComboBoxDropDownBorderBrush"] = new SolidColorBrush(cardBorderColor);
        
        // ListBox/ComboBox item resources
        resources["ListBoxItemForeground"] = new SolidColorBrush(textColor);
        resources["ListBoxItemBackgroundPointerOver"] = new SolidColorBrush(Color.FromArgb(15, textColor.R, textColor.G, textColor.B));
        resources["ListBoxItemBackgroundSelected"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundSelected));
        resources["ListBoxItemForegroundSelected"] = new SolidColorBrush(Colors.White);
        
        // CheckBox border - dark for light themes, white for dark themes
        var isLightTheme = theme.Name == "Prinzessin" || theme.Name == "Weiß";
        var checkBoxBorderColor = isLightTheme ? textColor : Colors.White;
        resources["CheckBoxBorderBrush"] = new SolidColorBrush(checkBoxBorderColor);
        
        // Override FluentTheme CheckBox resources
        resources["CheckBoxCheckBackgroundStrokeUnchecked"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeUncheckedPointerOver"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeUncheckedPressed"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeChecked"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeCheckedPointerOver"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeCheckedPressed"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeIndeterminate"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeIndeterminatePointerOver"] = new SolidColorBrush(checkBoxBorderColor);
        resources["CheckBoxCheckBackgroundStrokeIndeterminatePressed"] = new SolidColorBrush(checkBoxBorderColor);
    }

    private static Color ParseColor(string hexColor)
    {
        try
        {
            // Remove # if present
            var hex = hexColor.TrimStart('#');

            // Parse ARGB or RGB
            if (hex.Length == 8)
            {
                // ARGB format: #AARRGGBB
                byte a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                return Color.FromArgb(a, r, g, b);
            }
            else if (hex.Length == 6)
            {
                // RGB format: #RRGGBB
                byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return Color.FromArgb(255, r, g, b);
            }

            return Colors.White;
        }
        catch
        {
            return Colors.White;
        }
    }
}
