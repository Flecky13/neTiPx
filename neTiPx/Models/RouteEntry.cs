using neTiPx.Helpers;

namespace neTiPx.Models
{
    public sealed class RouteEntry : ObservableObject
    {
        private string _destination = string.Empty;
        private string _subnetMask = string.Empty;
        private string _gateway = string.Empty;
        private int _metric = 1;

        public string Destination
        {
            get => _destination;
            set => SetProperty(ref _destination, value);
        }

        public string SubnetMask
        {
            get => _subnetMask;
            set => SetProperty(ref _subnetMask, value);
        }

        public string Gateway
        {
            get => _gateway;
            set => SetProperty(ref _gateway, value);
        }

        public int Metric
        {
            get => _metric;
            set => SetProperty(ref _metric, value);
        }
    }
}
