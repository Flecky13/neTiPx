using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.Core.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class HoverViewModel : ObservableObject
{
    private readonly AdapterStore _adapterStore;

    [ObservableProperty]
    private string _publicIp = "-";

    [ObservableProperty]
    private string _nic1Name = "";

    [ObservableProperty]
    private string _nic1Ipv4 = "";

    [ObservableProperty]
    private string _nic1Gateway4 = "";

    [ObservableProperty]
    private string _nic1Dns4 = "";

    [ObservableProperty]
    private string _nic1Ipv6 = "";

    [ObservableProperty]
    private string _nic1Gateway6 = "";

    [ObservableProperty]
    private string _nic1Dns6 = "";

    [ObservableProperty]
    private bool _hasNic1Ipv6;

    [ObservableProperty]
    private string _nic2Name = "";

    [ObservableProperty]
    private string _nic2Ipv4 = "";

    [ObservableProperty]
    private string _nic2Gateway4 = "";

    [ObservableProperty]
    private string _nic2Dns4 = "";

    [ObservableProperty]
    private string _nic2Ipv6 = "";

    [ObservableProperty]
    private string _nic2Gateway6 = "";

    [ObservableProperty]
    private string _nic2Dns6 = "";

    [ObservableProperty]
    private bool _hasNic2Ipv6;

    [ObservableProperty]
    private bool _hasNic2;

    public HoverViewModel()
    {
        _adapterStore = new AdapterStore();
    }

    public Task RefreshAsync()
    {
        var adapterSettings = _adapterStore.ReadAdapters();
        var adapter1 = adapterSettings.PrimaryAdapter;
        var adapter2 = adapterSettings.SecondaryAdapter;

        // Simplified for now - you can implement these services later
        PublicIp = "-";
        
        string[,]? nic1 = null;
        string[,]? nic2 = null;

        ParseNetworkInfo(nic1, out var nic1Name, out var nic1Ipv4, out var nic1Gw4, out var nic1Dns4,
            out var nic1Ipv6, out var nic1Gw6, out var nic1Dns6, out var hasNic1Ipv6);

        ParseNetworkInfo(nic2, out var nic2Name, out var nic2Ipv4, out var nic2Gw4, out var nic2Dns4,
            out var nic2Ipv6, out var nic2Gw6, out var nic2Dns6, out var hasNic2Ipv6);

        Nic1Name = nic1Name;
        Nic1Ipv4 = nic1Ipv4;
        Nic1Gateway4 = nic1Gw4;
        Nic1Dns4 = nic1Dns4;
        Nic1Ipv6 = nic1Ipv6;
        Nic1Gateway6 = nic1Gw6;
        Nic1Dns6 = nic1Dns6;
        HasNic1Ipv6 = hasNic1Ipv6;

        Nic2Name = nic2Name;
        Nic2Ipv4 = nic2Ipv4;
        Nic2Gateway4 = nic2Gw4;
        Nic2Dns4 = nic2Dns4;
        Nic2Ipv6 = nic2Ipv6;
        Nic2Gateway6 = nic2Gw6;
        Nic2Dns6 = nic2Dns6;
        HasNic2Ipv6 = hasNic2Ipv6;

        HasNic2 = !string.IsNullOrWhiteSpace(nic2Name);
        
        return Task.CompletedTask;
    }

    private static void ParseNetworkInfo(string[,]? info, out string name, out string ipv4, out string gw4,
        out string dns4, out string ipv6, out string gw6, out string dns6, out bool hasIpv6)
    {
        name = "";
        ipv4 = "-";
        gw4 = "-";
        dns4 = "-";
        ipv6 = "-";
        gw6 = "-";
        dns6 = "-";
        hasIpv6 = false;

        if (info == null)
            return;

        int rows = info.GetLength(0);
        for (int i = 0; i < rows; i++)
        {
            var label = info[i, 0] ?? string.Empty;
            var value = info[i, 1] ?? string.Empty;

            if (label.Equals("Name", System.StringComparison.OrdinalIgnoreCase))
                name = value;
            else if (label.Equals("IPv4", System.StringComparison.OrdinalIgnoreCase))
                ipv4 = value;
            else if (label.Equals("Gateway4", System.StringComparison.OrdinalIgnoreCase))
                gw4 = value;
            else if (label.Equals("DNS4", System.StringComparison.OrdinalIgnoreCase))
                dns4 = value;
            else if (label.Equals("IPv6", System.StringComparison.OrdinalIgnoreCase))
            {
                ipv6 = value;
                hasIpv6 = !value.Equals("-") && !value.Contains("Keine");
            }
            else if (label.Equals("Gateway6", System.StringComparison.OrdinalIgnoreCase))
                gw6 = value;
            else if (label.Equals("DNS6", System.StringComparison.OrdinalIgnoreCase))
                dns6 = value;
        }
    }
}
