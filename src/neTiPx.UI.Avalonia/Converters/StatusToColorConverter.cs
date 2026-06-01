using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Converters;

public class StatusToColorConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count > 0 && values[0] is GatewayStatusKind statusKind)
        {
            return statusKind switch
            {
                GatewayStatusKind.Good => Color.FromRgb(0, 128, 0),      // Green
                GatewayStatusKind.Warning => Color.FromRgb(255, 165, 0), // Orange
                GatewayStatusKind.Bad => Color.FromRgb(255, 0, 0),       // Red
                _ => Color.FromRgb(128, 128, 128)                        // Gray for Unknown
            };
        }
        
        return Color.FromRgb(128, 128, 128);
    }
    
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
