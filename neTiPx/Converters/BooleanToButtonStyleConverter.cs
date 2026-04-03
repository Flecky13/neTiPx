using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace neTiPx.Converters
{
    public sealed class BooleanToButtonStyleConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not bool isHighlighted || !isHighlighted)
            {
                return DependencyProperty.UnsetValue;
            }

            if (parameter is not string styleKey || string.IsNullOrWhiteSpace(styleKey))
            {
                return DependencyProperty.UnsetValue;
            }

            if (Application.Current.Resources.TryGetValue(styleKey, out var style))
            {
                return style;
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
