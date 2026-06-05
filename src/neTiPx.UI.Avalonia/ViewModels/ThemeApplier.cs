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
        
        // Selected Background: Verwende die Akzentfarbe mit reduzierter Transparenz für dezentere Hinterlegung
        var selectedColor = ParseColor(theme.NavigationViewItemForegroundSelected);
        resources["ListBoxItemBackgroundSelected"] = new SolidColorBrush(Color.FromArgb(30, selectedColor.R, selectedColor.G, selectedColor.B));
        resources["ListBoxItemForegroundSelected"] = new SolidColorBrush(ParseColor(theme.NavigationViewItemForegroundSelected));
        
        // CheckBox border - use text color which is already correct for each theme
        resources["CheckBoxBorderBrush"] = new SolidColorBrush(textColor);
        
        // Override FluentTheme CheckBox resources - use text color
        resources["CheckBoxCheckBackgroundStrokeUnchecked"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeUncheckedPointerOver"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeUncheckedPressed"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeChecked"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeCheckedPointerOver"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeCheckedPressed"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeIndeterminate"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeIndeterminatePointerOver"] = new SolidColorBrush(textColor);
        resources["CheckBoxCheckBackgroundStrokeIndeterminatePressed"] = new SolidColorBrush(textColor);
        
        // NumericUpDown button arrows - use text color
        resources["TextControlButtonForeground"] = new SolidColorBrush(textColor);
        resources["TextControlButtonForegroundPointerOver"] = new SolidColorBrush(textColor);
        resources["TextControlButtonForegroundPressed"] = new SolidColorBrush(textColor);
        resources["RepeatButtonForeground"] = new SolidColorBrush(textColor);
        resources["RepeatButtonForegroundPointerOver"] = new SolidColorBrush(textColor);
        resources["RepeatButtonForegroundPressed"] = new SolidColorBrush(textColor);
        resources["NumericUpDownButtonForeground"] = new SolidColorBrush(textColor);
        resources["NumericUpDownButtonForegroundPointerOver"] = new SolidColorBrush(textColor);
        resources["NumericUpDownButtonForegroundPressed"] = new SolidColorBrush(textColor);
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
