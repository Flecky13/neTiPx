using Microsoft.UI.Xaml.Data;
using System;

namespace neTiPx.Converters
{
    public sealed class BooleanToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Aktiv" : "Inaktiv";
            }
            return "Inaktiv";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
