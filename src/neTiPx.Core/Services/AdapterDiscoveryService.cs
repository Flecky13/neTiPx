using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace neTiPx.Core.Services;

/// <summary>
/// Helper service to discover available network adapters.
/// </summary>
public sealed class AdapterDiscoveryService
{
    /// <summary>
    /// Gets all active network adapters (those that are up and have an IP address).
    /// </summary>
    public NetworkInterface[] GetActiveAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .Where(adapter =>
            {
                var props = adapter.GetIPProperties();
                var hasIpv4 = props.UnicastAddresses
                    .Any(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);
                return hasIpv4;
            })
            .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .OrderByDescending(adapter => adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .ThenBy(adapter => adapter.Name)
            .ToArray();
    }

    /// <summary>
    /// Gets the name of an adapter (combining name and description for clarity).
    /// </summary>
    public string GetAdapterDisplayName(NetworkInterface adapter)
    {
        if (string.IsNullOrWhiteSpace(adapter.Description) || adapter.Name == adapter.Description)
        {
            return adapter.Name;
        }

        return $"{adapter.Name} - {adapter.Description}";
    }

    /// <summary>
    /// Tries to auto-detect primary and secondary adapters.
    /// Returns null if no adapters are found.
    /// </summary>
    public AdapterStore.AdapterSettings? AutoDetectAdapters()
    {
        var adapters = GetActiveAdapters();
        
        if (adapters.Length == 0)
        {
            return null;
        }

        var settings = new AdapterStore.AdapterSettings
        {
            PrimaryAdapter = adapters[0].Name
        };

        if (adapters.Length > 1)
        {
            settings.SecondaryAdapter = adapters[1].Name;
        }

        return settings;
    }
}
