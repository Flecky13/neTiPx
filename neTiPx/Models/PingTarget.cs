using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using System.Net;
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
        private bool _isPingEnabled = true;
        private string _source = string.Empty;
        private string _resolvedAddressIpv4 = string.Empty;
        private string _resolvedAddressIpv6 = string.Empty;

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

        public bool IsPingEnabled
        {
            get => _isPingEnabled;
            set => SetProperty(ref _isPingEnabled, value);
        }

        public string Source
        {
            get => _source;
            set
            {
                SetProperty(ref _source, value);
                OnPropertyChanged(nameof(StatisticsIpv4));
                OnPropertyChanged(nameof(StatisticsIpv6));
            }
        }

        public string ResolvedAddressIpv4
        {
            get => _resolvedAddressIpv4;
            set
            {
                SetProperty(ref _resolvedAddressIpv4, value);
                OnPropertyChanged(nameof(StatisticsIpv4));
            }
        }

        public string ResolvedAddressIpv6
        {
            get => _resolvedAddressIpv6;
            set
            {
                SetProperty(ref _resolvedAddressIpv6, value);
                OnPropertyChanged(nameof(StatisticsIpv6));
            }
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
                var resolvedIpInfo = GetResolvedIpInfo(_resolvedAddressIpv4);
                if (_pingCountIpv4 == 0)
                {
                    return string.IsNullOrEmpty(resolvedIpInfo)
                        ? "IPv4: 0"
                        : $"IPv4: 0 | {resolvedIpInfo}";
                }

                var avg = _responseTimesIpv4.Count > 0 ? _responseTimesIpv4.Average() : 0;
                var max = _responseTimesIpv4.Count > 0 ? _responseTimesIpv4.Max() : 0;
                return string.IsNullOrEmpty(resolvedIpInfo)
                    ? $"IPv4: {_pingCountIpv4} | Ø: {avg:F1}ms | Max: {max}ms | Timeouts: {_timeoutCountIpv4}"
                    : $"IPv4: {_pingCountIpv4} | {resolvedIpInfo} | Ø: {avg:F1}ms | Max: {max}ms | Timeouts: {_timeoutCountIpv4}";
            }
        }

        public string StatisticsIpv6
        {
            get
            {
                var resolvedIpInfo = GetResolvedIpInfo(_resolvedAddressIpv6);
                if (_pingCountIpv6 == 0)
                {
                    return string.IsNullOrEmpty(resolvedIpInfo)
                        ? "IPv6: 0"
                        : $"IPv6: 0 | {resolvedIpInfo}";
                }

                var avg = _responseTimesIpv6.Count > 0 ? _responseTimesIpv6.Average() : 0;
                var max = _responseTimesIpv6.Count > 0 ? _responseTimesIpv6.Max() : 0;
                return string.IsNullOrEmpty(resolvedIpInfo)
                    ? $"IPv6: {_pingCountIpv6} | Ø: {avg:F1}ms | Max: {max}ms | Timeouts: {_timeoutCountIpv6}"
                    : $"IPv6: {_pingCountIpv6} | {resolvedIpInfo} | Ø: {avg:F1}ms | Max: {max}ms | Timeouts: {_timeoutCountIpv6}";
            }
        }

        private string GetResolvedIpInfo(string resolvedIp)
        {
            var isDnsSource = !string.IsNullOrWhiteSpace(_source) && _source.Trim().StartsWith("DN", System.StringComparison.OrdinalIgnoreCase);
            var isHostnameTarget = !IPAddress.TryParse(_target, out _);

            return (isDnsSource || isHostnameTarget) && !string.IsNullOrWhiteSpace(resolvedIp)
                ? $"IP: {resolvedIp}"
                : string.Empty;
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
