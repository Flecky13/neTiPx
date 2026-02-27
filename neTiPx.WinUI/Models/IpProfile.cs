using System.Collections.ObjectModel;
using neTiPx.WinUI.Helpers;

namespace neTiPx.WinUI.Models
{
    public sealed class IpProfile : ObservableObject
    {
        private string _name = "IP #1";
        private string _adapterName = string.Empty;
        private string _mode = "DHCP";
        private string _gateway = string.Empty;
        private string _dns1 = string.Empty;
        private string _dns2 = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string AdapterName
        {
            get => _adapterName;
            set => SetProperty(ref _adapterName, value);
        }

        public string Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public string Gateway
        {
            get => _gateway;
            set => SetProperty(ref _gateway, value);
        }

        public string Dns1
        {
            get => _dns1;
            set => SetProperty(ref _dns1, value);
        }

        public string Dns2
        {
            get => _dns2;
            set => SetProperty(ref _dns2, value);
        }

        // Legacy property for backward compatibility
        public string Dns
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_dns1))
                    return _dns2;
                if (string.IsNullOrWhiteSpace(_dns2))
                    return _dns1;
                return $"{_dns1},{_dns2}";
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _dns1 = string.Empty;
                    _dns2 = string.Empty;
                    return;
                }

                var parts = value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                _dns1 = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                _dns2 = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            }
        }

        public ObservableCollection<IpAddressEntry> IpAddresses { get; } = new ObservableCollection<IpAddressEntry>();
    }
}
