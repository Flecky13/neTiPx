using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace neTiPx.WinUI.Services
{
    public sealed class NetworkInfoService
    {
        private static readonly string[] InfoNames =
        {
            "Name",
            "MAC",
            "IPv4",
            "Gateway4",
            "DNS4",
            "IPv6",
            "Gateway6",
            "DNS6"
        };

        public string[,]? GetNetworkInfo(string adapterName)
        {
            try
            {
                var adapter = FindAdapter(adapterName);
                if (adapter == null)
                {
                    return null;
                }

                if (adapter.OperationalStatus != OperationalStatus.Up)
                {
                    var infosDown = new string[3, 2];
                    int idx = 0;

                    infosDown[idx, 0] = "Name";
                    infosDown[idx++, 1] = adapter.Name;

                    infosDown[idx, 0] = "MAC";
                    infosDown[idx++, 1] = string.Join(":", adapter.GetPhysicalAddress()
                        .GetAddressBytes()
                        .Select(b => b.ToString("X2")));

                    infosDown[idx, 0] = "Status";
                    infosDown[idx++, 1] = "Keine Verbindung";

                    return infosDown;
                }

                var props = adapter.GetIPProperties();
                var infos = new string[InfoNames.Length, 2];
                int index = 0;

                infos[index, 0] = "Name";
                infos[index++, 1] = adapter.Name;

                infos[index, 0] = "MAC";
                infos[index++, 1] = string.Join(":", adapter.GetPhysicalAddress()
                    .GetAddressBytes()
                    .Select(b => b.ToString("X2")));

                var ipv4 = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address.ToString())
                    .ToList();
                infos[index, 0] = "IPv4";
                infos[index++, 1] = ipv4.Any() ? string.Join(Environment.NewLine, ipv4) : "-";

                var gw4 = props.GatewayAddresses
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(g => g.Address.ToString())
                    .ToList();
                infos[index, 0] = "Gateway4";
                infos[index++, 1] = gw4.Any() ? string.Join(Environment.NewLine, gw4) : "-";

                var dns4 = props.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                    .Select(d => d.ToString())
                    .ToList();
                infos[index, 0] = "DNS4";
                infos[index++, 1] = dns4.Any() ? string.Join(Environment.NewLine, dns4) : "-";

                var ipv6 = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(a => a.Address.ToString())
                    .ToList();
                infos[index, 0] = "IPv6";
                infos[index++, 1] = ipv6.Any() ? string.Join(Environment.NewLine, ipv6) : "-";

                var gw6 = props.GatewayAddresses
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(g => g.Address.ToString())
                    .ToList();
                infos[index, 0] = "Gateway6";
                infos[index++, 1] = gw6.Any() ? string.Join(Environment.NewLine, gw6) : "-";

                var dns6 = props.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetworkV6
                        && !d.IsIPv6SiteLocal)
                    .Select(d => d.ToString())
                    .ToList();
                infos[index, 0] = "DNS6";
                infos[index++, 1] = dns6.Any() ? string.Join(Environment.NewLine, dns6) : "-";

                return infos;
            }
            catch
            {
                return null;
            }
        }

        public class Ipv4Config
        {
            public string? Gateway { get; set; }
            public string? Dns1 { get; set; }
            public string? Dns2 { get; set; }
            public List<(string IpAddress, string SubnetMask)> IpAddresses { get; set; } = new();
        }

        public Ipv4Config? GetIpv4Config(string adapterName)
        {
            try
            {
                var adapter = FindAdapter(adapterName);
                if (adapter == null || adapter.OperationalStatus != OperationalStatus.Up)
                {
                    return null;
                }

                var props = adapter.GetIPProperties();
                var config = new Ipv4Config();

                // Get Gateway
                var gateways = props.GatewayAddresses
                    .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(g => g.Address.ToString())
                    .ToList();
                if (gateways.Any())
                {
                    config.Gateway = gateways.First();
                }

                // Get DNS Servers
                var dnsServers = props.DnsAddresses
                    .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                    .Select(d => d.ToString())
                    .ToList();
                if (dnsServers.Count > 0)
                    config.Dns1 = dnsServers[0];
                if (dnsServers.Count > 1)
                    config.Dns2 = dnsServers[1];

                // Get IP Addresses and Subnet Masks
                var unicastAddresses = props.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .ToList();

                foreach (var addr in unicastAddresses)
                {
                    var ipAddress = addr.Address.ToString();
                    var subnetMask = addr.IPv4Mask?.ToString() ?? "255.255.255.0";
                    config.IpAddresses.Add((ipAddress, subnetMask));
                }

                return config;
            }
            catch
            {
                return null;
            }
        }

        private static NetworkInterface? FindAdapter(string adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
            {
                return null;
            }

            return NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(a => a.Name.Equals(adapterName, StringComparison.OrdinalIgnoreCase)
                    || a.Description.Equals(adapterName, StringComparison.OrdinalIgnoreCase)
                    || (a.Name + " - " + a.Description).Equals(adapterName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
