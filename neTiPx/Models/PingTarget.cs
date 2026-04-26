using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;

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

        private const int TrendSampleCount = 24;
        private const double TrendWidth = 84d;
        private const double TrendHeight = 34d;
        private const double TrendTopPadding = 2d;
        private const double TrendBottomPadding = 2d;
        private const double TrendMinMetricSpan = 8d;

        private readonly Queue<double> _trendHistoryIpv4 = new Queue<double>();
        private readonly Queue<double> _trendHistoryIpv6 = new Queue<double>();
        private bool _trendPulseIpv4;
        private bool _trendPulseIpv6;
        private PointCollection _trendPointsIpv4 = CreateFlatTrendPoints();
        private PointCollection _trendPointsIpv6 = CreateFlatTrendPoints();

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

        public PointCollection TrendPointsIpv4
        {
            get => _trendPointsIpv4;
            set => SetProperty(ref _trendPointsIpv4, value);
        }

        public PointCollection TrendPointsIpv6
        {
            get => _trendPointsIpv6;
            set => SetProperty(ref _trendPointsIpv6, value);
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

        public void UpdateTrendIpv4(long? responseTimeMs, bool isBad)
        {
            var metric = ResolveTrendMetric(responseTimeMs, isBad, ref _trendPulseIpv4);
            PushTrendSample(_trendHistoryIpv4, metric);
            TrendPointsIpv4 = BuildTrendPoints(_trendHistoryIpv4);
        }

        public void ResetTrendIpv4()
        {
            _trendHistoryIpv4.Clear();
            _trendPulseIpv4 = false;
            TrendPointsIpv4 = CreateFlatTrendPoints();
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

        public void UpdateTrendIpv6(long? responseTimeMs, bool isBad)
        {
            var metric = ResolveTrendMetric(responseTimeMs, isBad, ref _trendPulseIpv6);
            PushTrendSample(_trendHistoryIpv6, metric);
            TrendPointsIpv6 = BuildTrendPoints(_trendHistoryIpv6);
        }

        public void ResetTrendIpv6()
        {
            _trendHistoryIpv6.Clear();
            _trendPulseIpv6 = false;
            TrendPointsIpv6 = CreateFlatTrendPoints();
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

        private static PointCollection CreateFlatTrendPoints()
        {
            var points = new PointCollection();
            var step = TrendWidth / (TrendSampleCount - 1);
            var centerY = TrendHeight / 2d;
            for (int i = 0; i < TrendSampleCount; i++)
            {
                points.Add(new Point(i * step, centerY));
            }
            return points;
        }

        private static double ResolveTrendMetric(long? responseTimeMs, bool isBad, ref bool pulseState)
        {
            if (responseTimeMs.HasValue)
            {
                return responseTimeMs.Value;
            }

            pulseState = !pulseState;
            if (isBad)
            {
                return pulseState ? 260d : 120d;
            }

            return pulseState ? 55d : 45d;
        }

        private static void PushTrendSample(Queue<double> history, double value)
        {
            history.Enqueue(value);
            while (history.Count > TrendSampleCount)
            {
                history.Dequeue();
            }
        }

        private static PointCollection BuildTrendPoints(Queue<double> history)
        {
            var points = new PointCollection();
            if (history.Count == 0)
            {
                return CreateFlatTrendPoints();
            }

            var samples = history.ToArray();
            var step = TrendWidth / (TrendSampleCount - 1);
            var leading = TrendSampleCount - samples.Length;

            var minMetric = samples.Min();
            var maxMetric = samples.Max();
            if ((maxMetric - minMetric) < TrendMinMetricSpan)
            {
                var center = (maxMetric + minMetric) / 2d;
                minMetric = center - (TrendMinMetricSpan / 2d);
                maxMetric = center + (TrendMinMetricSpan / 2d);
            }

            var metricRange = maxMetric - minMetric;
            var usableHeight = TrendHeight - TrendTopPadding - TrendBottomPadding;
            var centerY = TrendHeight / 2d;

            double MapMetricToY(double metric)
            {
                var normalized = (metric - minMetric) / metricRange;
                return TrendTopPadding + ((1d - normalized) * usableHeight);
            }

            for (int i = 0; i < leading; i++)
            {
                points.Add(new Point(i * step, centerY));
            }

            for (int i = 0; i < samples.Length; i++)
            {
                points.Add(new Point((i + leading) * step, MapMetricToY(samples[i])));
            }

            return points;
        }
    }

}
