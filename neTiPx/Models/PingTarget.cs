using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;

namespace neTiPx.Models
{
    public class PingTarget : ObservableObject
    {
        private string _target = string.Empty;
        private string _responseTimeIpv4 = string.Empty;
        private string _responseTimeIpv6 = string.Empty;
        private SolidColorBrush _statusColorIpv4 = new SolidColorBrush(Colors.Gray);
        private SolidColorBrush _statusColorIpv6 = new SolidColorBrush(Colors.Gray);
        private int _intervalSeconds = 5;
        private Visibility _showIPv4 = Visibility.Visible;
        private Visibility _showIPv6 = Visibility.Visible;

        public string Target
        {
            get => _target;
            set => SetProperty(ref _target, value);
        }

        public string ResponseTimeIpv4
        {
            get => _responseTimeIpv4;
            set => SetProperty(ref _responseTimeIpv4, value);
        }

        public string ResponseTimeIpv6
        {
            get => _responseTimeIpv6;
            set => SetProperty(ref _responseTimeIpv6, value);
        }

        public SolidColorBrush StatusColorIpv4
        {
            get => _statusColorIpv4;
            set => SetProperty(ref _statusColorIpv4, value);
        }

        public SolidColorBrush StatusColorIpv6
        {
            get => _statusColorIpv6;
            set => SetProperty(ref _statusColorIpv6, value);
        }

        public int IntervalSeconds
        {
            get => _intervalSeconds;
            set => SetProperty(ref _intervalSeconds, value);
        }

        public Visibility ShowIPv4
        {
            get => _showIPv4;
            set => SetProperty(ref _showIPv4, value);
        }

        public Visibility ShowIPv6
        {
            get => _showIPv6;
            set => SetProperty(ref _showIPv6, value);
        }

        // Legacy Eigenschaft für Rückwärtskompatibilität
        public string ResponseTime
        {
            get => $"IPv4: {ResponseTimeIpv4} | IPv6: {ResponseTimeIpv6}";
        }

        public SolidColorBrush StatusColor
        {
            get => _statusColorIpv4;
        }
    }
}
