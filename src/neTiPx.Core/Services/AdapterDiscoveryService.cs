using System;
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
    /// macOS-interne virtuelle Interfaces, die keine echten Netzwerkadapter sind
    /// (Apple-Debug-Bridges, AirDrop, Personal Hotspot, VPN-Tunnel usw.)
    /// </summary>
    private static readonly string[] MacVirtualInterfacePrefixes =
    {
        "anpi", "ap", "awdl", "llw", "bridge", "gif", "stf", "utun", "pktap", "feth", "vmenet"
    };

    /// <summary>
    /// Prüft, ob ein Interface ein plattform-internes virtuelles Interface ist,
    /// das dem Benutzer nicht als Netzwerkadapter angeboten werden soll.
    /// </summary>
    public static bool IsPlatformVirtualInterface(NetworkInterface adapter)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        var name = adapter.Name;
        return MacVirtualInterfacePrefixes.Any(prefix =>
            name.Length > prefix.Length
            && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(name[prefix.Length]));
    }

    /// <summary>
    /// Gets all adapters that make sense to offer for selection:
    /// no loopback, no platform-virtual interfaces; connected adapters first.
    /// </summary>
    public NetworkInterface[] GetSelectableAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(adapter => !IsPlatformVirtualInterface(adapter))
            .OrderByDescending(HasIpv4Address)
            .ThenByDescending(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .ThenBy(adapter => adapter.Name)
            .ToArray();
    }

    private static bool HasIpv4Address(NetworkInterface adapter)
    {
        try
        {
            return adapter.GetIPProperties().UnicastAddresses
                .Any(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets all active network adapters (those that are up and have an IP address).
    /// </summary>
    public NetworkInterface[] GetActiveAdapters()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
            .Where(HasIpv4Address)
            .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Where(adapter => !IsPlatformVirtualInterface(adapter))
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
