using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace neTiPx.Converters
{
    public sealed class BooleanToEnabledContentBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var isEnabled = value is bool flag && flag;
            var key = isEnabled ? "TextFillColorPrimaryBrush" : "TextFillColorDisabledBrush";

            if (Application.Current.Resources.TryGetValue(key, out var brush) && brush is Brush typedBrush)
            {
                return typedBrush;
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
