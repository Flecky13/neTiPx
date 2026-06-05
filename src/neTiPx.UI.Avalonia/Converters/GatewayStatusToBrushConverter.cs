using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Converters;

public class GatewayStatusToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GatewayStatusKind statusKind)
        {
            return statusKind switch
            {
                GatewayStatusKind.Good => new SolidColorBrush(Color.FromRgb(34, 139, 34)),    // ForestGreen
                GatewayStatusKind.Warning => new SolidColorBrush(Color.FromRgb(255, 165, 0)), // Orange
                GatewayStatusKind.Bad => new SolidColorBrush(Color.FromRgb(205, 92, 92)),     // IndianRed
                _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))                        // Gray
            };
        }
        
        return new SolidColorBrush(Color.FromRgb(128, 128, 128));
    }
    
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
