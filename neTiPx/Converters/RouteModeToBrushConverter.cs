using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using neTiPx.Models;

namespace neTiPx.Converters
{
    public sealed class RouteModeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isAddMode = false;
            bool routesEnabled = false;
            var mode = parameter?.ToString() ?? string.Empty;

            if (value is IpProfile profile)
            {
                isAddMode = profile.AddRoutesOnApply;
                routesEnabled = profile.RoutesEnabled;
            }
            else if (value is bool b)
            {
                isAddMode = b;
                routesEnabled = true;
            }

            if (string.Equals(mode, "count", StringComparison.OrdinalIgnoreCase))
            {
                var countKey = routesEnabled ? "RouteModeCountBrush" : "RouteModeDisabledBrush";
                if (Application.Current.Resources.TryGetValue(countKey, out var countBrush) && countBrush is Brush countResult)
                {
                    return countResult;
                }

                var fallbackCountKey = routesEnabled ? "TextFillColorPrimaryBrush" : "TextFillColorDisabledBrush";
                if (Application.Current.Resources.TryGetValue(fallbackCountKey, out var fallbackCountBrush) && fallbackCountBrush is Brush fallbackCountResult)
                {
                    return fallbackCountResult;
                }

                return new SolidColorBrush(routesEnabled ? Microsoft.UI.Colors.Black : Microsoft.UI.Colors.Gray);
            }

            bool highlightAddMode = string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase);
            bool isActive = isAddMode == highlightAddMode;

            string key;
            if (!routesEnabled)
            {
                key = "RouteModeDisabledBrush";
            }
            else
            {
                key = isActive ? "RouteModeActiveBrush" : "RouteModeInactiveBrush";
            }

            if (Application.Current.Resources.TryGetValue(key, out var brush) && brush is Brush result)
            {
                return result;
            }

            var fallbackKey = !routesEnabled
                ? "TextFillColorDisabledBrush"
                : isActive ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush";

            if (Application.Current.Resources.TryGetValue(fallbackKey, out var fallbackBrush) && fallbackBrush is Brush fallbackResult)
            {
                return fallbackResult;
            }

            if (!routesEnabled)
            {
                return new SolidColorBrush(Microsoft.UI.Colors.Gray);
            }

            return new SolidColorBrush(isActive ? Microsoft.UI.Colors.DodgerBlue : Microsoft.UI.Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
