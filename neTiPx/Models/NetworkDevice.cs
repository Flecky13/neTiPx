using neTiPx.Helpers;

namespace neTiPx.Models
{
    public class NetworkDevice : ObservableObject
    {
        private string _ipAddress = string.Empty;
        private string _macAddress = string.Empty;
        private string _hostname = string.Empty;

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public string MacAddress
        {
            get => _macAddress;
            set => SetProperty(ref _macAddress, value);
        }

        public string Hostname
        {
            get => _hostname;
            set => SetProperty(ref _hostname, value);
        }
    }
}
