using neTiPx.WinUI.Helpers;

namespace neTiPx.WinUI.Models
{
    public sealed class IpAddressEntry : ObservableObject
    {
        private string _ipAddress = string.Empty;
        private string _subnetMask = string.Empty;

        public string IpAddress
        {
            get => _ipAddress;
            set => SetProperty(ref _ipAddress, value);
        }

        public string SubnetMask
        {
            get => _subnetMask;
            set => SetProperty(ref _subnetMask, value);
        }
    }
}
