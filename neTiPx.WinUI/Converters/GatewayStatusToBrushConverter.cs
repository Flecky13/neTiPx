using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using neTiPx.WinUI.Models;

namespace neTiPx.WinUI.Converters
{
    public sealed class GatewayStatusToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not GatewayStatusKind status)
            {
                return new SolidColorBrush(Colors.Gray);
            }

            return status switch
            {
                GatewayStatusKind.Good => new SolidColorBrush(Colors.ForestGreen),
                GatewayStatusKind.Warning => new SolidColorBrush(Colors.Orange),
                GatewayStatusKind.Bad => new SolidColorBrush(Colors.IndianRed),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
