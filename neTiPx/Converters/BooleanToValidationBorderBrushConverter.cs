using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System;

namespace neTiPx.Converters
{
    public sealed class BooleanToValidationBorderBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 211, 52, 69));

        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool hasError && hasError)
            {
                return ErrorBrush;
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
