using System;
using neTiPx.Helpers;

namespace neTiPx.Models
{
    public sealed class IpAddressEntry : ObservableObject
    {
        private string _ipAddress = string.Empty;
        private string _subnetMask = string.Empty;
        private bool _hasIpAddressError;
        private bool _hasSubnetMaskError;
        private bool _canRemove;

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

        public bool HasIpAddressError
        {
            get => _hasIpAddressError;
            set => SetProperty(ref _hasIpAddressError, value);
        }

        public bool HasSubnetMaskError
        {
            get => _hasSubnetMaskError;
            set => SetProperty(ref _hasSubnetMaskError, value);
        }

        public bool CanRemove
        {
            get => _canRemove;
            set => SetProperty(ref _canRemove, value);
        }

    }
}
