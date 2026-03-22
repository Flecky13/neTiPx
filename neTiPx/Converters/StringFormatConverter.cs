using System;
using Microsoft.UI.Xaml.Data;

namespace neTiPx.Converters
{
    public sealed class StringFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringValue && parameter is string format)
            {
                try
                {
                    return string.Format(format, stringValue);
                }
                catch
                {
                    return value;
                }
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
