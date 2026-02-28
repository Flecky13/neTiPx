using System;
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
            set
            {
                // Convert CIDR suffix to subnet mask if needed
                var normalized = NormalizeSubnetMask(value);
                SetProperty(ref _subnetMask, normalized);
            }
        }

        private static string NormalizeSubnetMask(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            input = input.Trim();

            // Check if input is CIDR notation (e.g., /24 or 24)
            if (input.StartsWith("/"))
            {
                input = input.Substring(1);
            }

            if (int.TryParse(input, out var cidr) && cidr >= 0 && cidr <= 32)
            {
                return CidrToSubnetMask(cidr);
            }

            // Return as-is if it's already in dotted decimal notation
            return input;
        }

        private static string CidrToSubnetMask(int cidr)
        {
            if (cidr < 0 || cidr > 32)
            {
                return "255.255.255.0";
            }

            uint mask = cidr == 0 ? 0 : 0xFFFFFFFF << (32 - cidr);
            byte[] bytes = new byte[4];
            bytes[0] = (byte)(mask >> 24);
            bytes[1] = (byte)(mask >> 16);
            bytes[2] = (byte)(mask >> 8);
            bytes[3] = (byte)mask;

            return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{bytes[3]}";
        }
    }
}
