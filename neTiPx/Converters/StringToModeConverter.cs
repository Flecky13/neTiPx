using Microsoft.UI.Xaml.Data;
using System;

namespace neTiPx.Converters
{
    public sealed class StringToModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var mode = value as string;
            var targetMode = parameter as string;
            return string.Equals(mode, targetMode, StringComparison.OrdinalIgnoreCase);
        }

        public object? ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isChecked && isChecked && parameter is string mode)
            {
                return mode;
            }
            return null;
        }
    }
}
