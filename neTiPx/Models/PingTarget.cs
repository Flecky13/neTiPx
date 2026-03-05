using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using System.Collections.Generic;
using System.Linq;

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

        // Statistiken IPv4
        private int _pingCountIpv4 = 0;
        private int _timeoutCountIpv4 = 0;
        private readonly List<long> _responseTimesIpv4 = new List<long>();

        // Statistiken IPv6
        private int _pingCountIpv6 = 0;
        private int _timeoutCountIpv6 = 0;
        private readonly List<long> _responseTimesIpv6 = new List<long>();

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

        // Statistiken IPv4
        public int PingCountIpv4
        {
            get => _pingCountIpv4;
            set
            {
                SetProperty(ref _pingCountIpv4, value);
                OnPropertyChanged(nameof(StatisticsIpv4));
            }
        }

        public int TimeoutCountIpv4
        {
            get => _timeoutCountIpv4;
            set
            {
                SetProperty(ref _timeoutCountIpv4, value);
                OnPropertyChanged(nameof(StatisticsIpv4));
            }
        }

        public void AddResponseTimeIpv4(long milliseconds)
        {
            _responseTimesIpv4.Add(milliseconds);
            OnPropertyChanged(nameof(StatisticsIpv4));
        }

        // Statistiken IPv6
        public int PingCountIpv6
        {
            get => _pingCountIpv6;
            set
            {
                SetProperty(ref _pingCountIpv6, value);
                OnPropertyChanged(nameof(StatisticsIpv6));
            }
        }

        public int TimeoutCountIpv6
        {
            get => _timeoutCountIpv6;
            set
            {
                SetProperty(ref _timeoutCountIpv6, value);
                OnPropertyChanged(nameof(StatisticsIpv6));
            }
        }

        public void AddResponseTimeIpv6(long milliseconds)
        {
            _responseTimesIpv6.Add(milliseconds);
            OnPropertyChanged(nameof(StatisticsIpv6));
        }

        // Formatierte Statistiken
        public string StatisticsIpv4
        {
            get
            {
                if (_pingCountIpv4 == 0) return "IPv4: 0";

                var avg = _responseTimesIpv4.Count > 0 ? _responseTimesIpv4.Average() : 0;
                var max = _responseTimesIpv4.Count > 0 ? _responseTimesIpv4.Max() : 0;
                return $"IPv4: {_pingCountIpv4} | Ø: {avg:F1}ms | Max: {max}ms | Timeouts: {_timeoutCountIpv4}";
            }
        }

        public string StatisticsIpv6
        {
            get
            {
                if (_pingCountIpv6 == 0) return "IPv6: 0";

                var avg = _responseTimesIpv6.Count > 0 ? _responseTimesIpv6.Average() : 0;
                var max = _responseTimesIpv6.Count > 0 ? _responseTimesIpv6.Max() : 0;
                return $"IPv6: {_pingCountIpv6} | Ø: {avg:F1}ms | Max: {max}ms | Timeouts: {_timeoutCountIpv6}";
            }
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
