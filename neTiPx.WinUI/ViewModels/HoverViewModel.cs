using System.Threading.Tasks;
using neTiPx.WinUI.Helpers;
using neTiPx.WinUI.Services;

namespace neTiPx.WinUI.ViewModels
{
    public sealed class HoverViewModel : ObservableObject
    {
        private readonly AdapterStore _adapterStore = new AdapterStore();
        private readonly InternetService _internetService = new InternetService();
        private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();

        private string _publicIp = "-";
        private string _nic1Name = "";
        private string _nic1Labels = "";
        private string _nic1Values = "";
        private string _nic2Name = "";
        private string _nic2Labels = "";
        private string _nic2Values = "";
        private bool _hasNic2;
        private bool _hasNic1Ipv6;
        private bool _hasNic2Ipv6;
        private string _nic1Ipv4 = "";
        private string _nic1Gateway4 = "";
        private string _nic1Dns4 = "";
        private string _nic1Ipv6 = "";
        private string _nic1Gateway6 = "";
        private string _nic1Dns6 = "";
        private string _nic2Ipv4 = "";
        private string _nic2Gateway4 = "";
        private string _nic2Dns4 = "";
        private string _nic2Ipv6 = "";
        private string _nic2Gateway6 = "";
        private string _nic2Dns6 = "";

        public string PublicIp
        {
            get => _publicIp;
            set => SetProperty(ref _publicIp, value);
        }

        public string Nic1Name
        {
            get => _nic1Name;
            set => SetProperty(ref _nic1Name, value);
        }

        public string Nic1Labels
        {
            get => _nic1Labels;
            set => SetProperty(ref _nic1Labels, value);
        }

        public string Nic1Values
        {
            get => _nic1Values;
            set => SetProperty(ref _nic1Values, value);
        }

        public string Nic2Name
        {
            get => _nic2Name;
            set => SetProperty(ref _nic2Name, value);
        }

        public string Nic2Labels
        {
            get => _nic2Labels;
            set => SetProperty(ref _nic2Labels, value);
        }

        public string Nic2Values
        {
            get => _nic2Values;
            set => SetProperty(ref _nic2Values, value);
        }

        public bool HasNic2
        {
            get => _hasNic2;
            set => SetProperty(ref _hasNic2, value);
        }

        public bool HasNic1Ipv6
        {
            get => _hasNic1Ipv6;
            set => SetProperty(ref _hasNic1Ipv6, value);
        }

        public bool HasNic2Ipv6
        {
            get => _hasNic2Ipv6;
            set => SetProperty(ref _hasNic2Ipv6, value);
        }

        public string Nic1Ipv4
        {
            get => _nic1Ipv4;
            set => SetProperty(ref _nic1Ipv4, value);
        }

        public string Nic1Gateway4
        {
            get => _nic1Gateway4;
            set => SetProperty(ref _nic1Gateway4, value);
        }

        public string Nic1Dns4
        {
            get => _nic1Dns4;
            set => SetProperty(ref _nic1Dns4, value);
        }

        public string Nic1Ipv6
        {
            get => _nic1Ipv6;
            set => SetProperty(ref _nic1Ipv6, value);
        }

        public string Nic1Gateway6
        {
            get => _nic1Gateway6;
            set => SetProperty(ref _nic1Gateway6, value);
        }

        public string Nic1Dns6
        {
            get => _nic1Dns6;
            set => SetProperty(ref _nic1Dns6, value);
        }

        public string Nic2Ipv4
        {
            get => _nic2Ipv4;
            set => SetProperty(ref _nic2Ipv4, value);
        }

        public string Nic2Gateway4
        {
            get => _nic2Gateway4;
            set => SetProperty(ref _nic2Gateway4, value);
        }

        public string Nic2Dns4
        {
            get => _nic2Dns4;
            set => SetProperty(ref _nic2Dns4, value);
        }

        public string Nic2Ipv6
        {
            get => _nic2Ipv6;
            set => SetProperty(ref _nic2Ipv6, value);
        }

        public string Nic2Gateway6
        {
            get => _nic2Gateway6;
            set => SetProperty(ref _nic2Gateway6, value);
        }

        public string Nic2Dns6
        {
            get => _nic2Dns6;
            set => SetProperty(ref _nic2Dns6, value);
        }

        public async Task RefreshAsync()
        {
            var adapterSettings = _adapterStore.ReadAdapters();
            var adapter1 = adapterSettings.PrimaryAdapter;
            var adapter2 = adapterSettings.SecondaryAdapter;

            PublicIp = await _internetService.LoadExternalIpAsync();

            var nic1 = _networkInfoService.GetNetworkInfo(adapter1 ?? string.Empty);
            var nic2 = _networkInfoService.GetNetworkInfo(adapter2 ?? string.Empty);

            ApplyNicInfo(nic1, out _nic1Name, out _nic1Labels, out _nic1Values);
            ApplyNicInfo(nic2, out _nic2Name, out _nic2Labels, out _nic2Values);

            OnPropertyChanged(nameof(Nic1Name));
            OnPropertyChanged(nameof(Nic2Name));

            // Parse detailed info for grid layout
            ParseNetworkInfo(nic1, out _nic1Ipv4, out _nic1Gateway4, out _nic1Dns4, out _nic1Ipv6, out _nic1Gateway6, out _nic1Dns6, out _hasNic1Ipv6);
            ParseNetworkInfo(nic2, out _nic2Ipv4, out _nic2Gateway4, out _nic2Dns4, out _nic2Ipv6, out _nic2Gateway6, out _nic2Dns6, out _hasNic2Ipv6);

            OnPropertyChanged(nameof(Nic1Ipv4));
            OnPropertyChanged(nameof(Nic1Gateway4));
            OnPropertyChanged(nameof(Nic1Dns4));
            OnPropertyChanged(nameof(Nic1Ipv6));
            OnPropertyChanged(nameof(Nic1Gateway6));
            OnPropertyChanged(nameof(Nic1Dns6));
            OnPropertyChanged(nameof(HasNic1Ipv6));

            OnPropertyChanged(nameof(Nic2Ipv4));
            OnPropertyChanged(nameof(Nic2Gateway4));
            OnPropertyChanged(nameof(Nic2Dns4));
            OnPropertyChanged(nameof(Nic2Ipv6));
            OnPropertyChanged(nameof(Nic2Gateway6));
            OnPropertyChanged(nameof(Nic2Dns6));
            OnPropertyChanged(nameof(HasNic2Ipv6));

            HasNic2 = !string.IsNullOrWhiteSpace(_nic2Name) || !string.IsNullOrWhiteSpace(_nic2Labels);
        }

        private static void ParseNetworkInfo(string[,]? info, out string ipv4, out string gw4, out string dns4,
            out string ipv6, out string gw6, out string dns6, out bool hasIpv6)
        {
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

                if (label.Equals("IPv4", System.StringComparison.OrdinalIgnoreCase))
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

        private static void ApplyNicInfo(string[,]? info, out string name, out string labels, out string values)
        {
            name = string.Empty;
            labels = string.Empty;
            values = string.Empty;

            if (info == null)
            {
                return;
            }

            var labelBuilder = new System.Text.StringBuilder();
            var valueBuilder = new System.Text.StringBuilder();
            int rows = info.GetLength(0);

            for (int i = 0; i < rows; i++)
            {
                var label = info[i, 0] ?? string.Empty;
                var value = info[i, 1] ?? string.Empty;

                if (label.Equals("Name", System.StringComparison.OrdinalIgnoreCase))
                {
                    name = value;
                }

                labelBuilder.AppendLine(label);
                valueBuilder.AppendLine(value.Replace("\r\n", "\n"));
                labelBuilder.AppendLine();
                valueBuilder.AppendLine();
            }

            labels = labelBuilder.ToString().TrimEnd('\r', '\n');
            values = valueBuilder.ToString().TrimEnd('\r', '\n');
        }
    }
}
