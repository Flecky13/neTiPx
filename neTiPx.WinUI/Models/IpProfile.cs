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
        private string _dns = string.Empty;

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

        public string Dns
        {
            get => _dns;
            set => SetProperty(ref _dns, value);
        }

        public ObservableCollection<IpAddressEntry> IpAddresses { get; } = new ObservableCollection<IpAddressEntry>();
    }
}
