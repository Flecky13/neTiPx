using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;

namespace neTiPx.Models
{
    public class PingTarget : ObservableObject
    {
        private string _target = string.Empty;
        private string _responseTime = string.Empty;
        private SolidColorBrush _statusColor = new SolidColorBrush(Colors.Gray);
        private int _intervalSeconds = 5;

        public string Target
        {
            get => _target;
            set => SetProperty(ref _target, value);
        }

        public string ResponseTime
        {
            get => _responseTime;
            set => SetProperty(ref _responseTime, value);
        }

        public SolidColorBrush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public int IntervalSeconds
        {
            get => _intervalSeconds;
            set => SetProperty(ref _intervalSeconds, value);
        }
    }
}
