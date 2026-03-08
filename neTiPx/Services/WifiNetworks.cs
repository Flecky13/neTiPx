using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

namespace neTiPx
{
    public class WifiNetwork
    {
        // Basic Info
        public string SSID { get; set; }
        public string BSSID { get; set; }
        public int SignalStrengthDbm { get; set; }
        public int SignalStrengthPercent { get; set; }

        // Extended Info
        public string NetworkType { get; set; }          // Infrastructure / Ad-Hoc
        public string PhyType { get; set; }              // 802.11a/b/g/n/ac/ax
        public uint LinkQuality { get; set; }            // 0-100
        public bool InRegDomain { get; set; }            // Regulatory Domain
        public ushort BeaconInterval { get; set; }       // ms
        public uint Channel { get; set; }                // Calculated from frequency
        public uint Frequency { get; set; }              // MHz
        public ushort Capabilities { get; set; }         // Capability flags
        public bool IsSecured { get; set; }              // Privacy bit from capabilities
        public string[] SupportedRates { get; set; }     // Mbps
        public DateTime LastSeen { get; set; }           // Timestamp
        public uint PhyId { get; set; }                  // Physical ID

        public string SignalSymbol
        {
            get
            {
                if (SignalStrengthPercent >= 75) return "📶";
                if (SignalStrengthPercent >= 50) return "📳";
                if (SignalStrengthPercent >= 25) return "📴";
                return "❌";
            }
        }

        public string SignalStrengthDisplay
        {
            get => $"{SignalStrengthPercent}% Signal";
        }

        public string Band
        {
            get
            {
                if (Frequency >= 5900) return "6G";
                if (Frequency >= 5000) return "5G";
                if (Frequency >= 2400) return "2.4G";
                return "---";
            }
        }

        public string SecurityType
        {
            get => IsSecured ? "Open" : "Secured";
        }

        public WifiNetwork(string ssid, string bssid, int signalDbm)
        {
            SSID = ssid;
            BSSID = bssid;
            SignalStrengthDbm = signalDbm;
            // Convert dBm to percentage (typical range: -30 to -90 dBm)
            SignalStrengthPercent = Math.Max(0, Math.Min(100, 2 * (signalDbm + 100)));
            SupportedRates = Array.Empty<string>();
            NetworkType = "Unknown";
            PhyType = "Unknown";
        }
    }

    public class WifiScanner
    {
        // Native WiFi API imports
        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern uint WlanGetNetworkBssList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pSsid, uint dot11BssType, bool bSecurityEnabled, IntPtr pReserved, out IntPtr ppWlanBssList);

        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        private const uint WLAN_API_VERSION = 2;
        private const uint DOT11_BSS_TYPE_ANY = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_INTERFACE_INFO_LIST
        {
            public uint dwNumberOfItems;
            public uint dwIndex;
        }

        // DOT11_SSID structure: 4 bytes length + 32 bytes data
        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        // WLAN_BSS_ENTRY - Struktur basierend auf realem Hex-Dump (nicht Vistumbler!)
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WLAN_BSS_ENTRY
        {
            public uint uSSIDLength;                  // Offset 0-3
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;                     // Offset 4-35
            public uint uPhyId;                       // Offset 36-39
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11BssId;                 // Offset 40-45
            public ushort padding1;                   // Offset 46-47 (PADDING!)
            public uint dot11BssType;                 // Offset 48-51
            public uint dot11BssPhyType;              // Offset 52-55
            public int lRssi;                         // Offset 56-59
            public uint uLinkQuality;                 // Offset 60-63
            public byte bInRegDomain;                 // Offset 64
            public byte padding2;                     // Offset 65 (padding)
            public ushort usBeaconPeriod;             // Offset 66-67
            public ulong ullTimestamp;                // Offset 68-75
            public ulong ullHostTimestamp;            // Offset 76-83
            public ushort usCapabilityInformation;    // Offset 84-85
            public ushort padding3;                   // Offset 86-87
            public uint ulChCenterFrequency;          // Offset 88-91
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 67)]
            public uint[] wlanRateSet;                // Offset 92-359 (268 bytes = 67 * 4)
            public uint ulIeOffset;                   // Offset 360-363
            public uint ulIeSize;                     // Offset 364-367
        }

        public static List<WifiNetwork> ScanWifiNetworks()
        {
            var networks = new List<WifiNetwork>();

            try
            {
                System.Diagnostics.Debug.WriteLine("[WifiScanner] Starting WiFi scan with Native API...");

                uint negotiatedVersion = 0;
                IntPtr clientHandle = IntPtr.Zero;

                // Open WLAN handle
                uint result = WlanOpenHandle(WLAN_API_VERSION, IntPtr.Zero, out negotiatedVersion, out clientHandle);
                if (result != 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[WifiScanner] WlanOpenHandle failed with code {result}");
                    return ScanWifiNetworksViaNetsh();
                }

                try
                {
                    // Enumerate interfaces
                    IntPtr interfaceListPtr = IntPtr.Zero;
                    result = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out interfaceListPtr);
                    if (result != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WifiScanner] WlanEnumInterfaces failed with code {result}");
                        return ScanWifiNetworksViaNetsh();
                    }

                    try
                    {
                        WLAN_INTERFACE_INFO_LIST interfaceList = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(interfaceListPtr);

                        // Iterate through each interface
                        for (uint i = 0; i < interfaceList.dwNumberOfItems; i++)
                        {
                            IntPtr interfaceInfoPtr = new IntPtr(interfaceListPtr.ToInt64() + Marshal.SizeOf(typeof(WLAN_INTERFACE_INFO_LIST)) + (int)(i * 532)); // 532 is size of WLAN_INTERFACE_INFO

                            // Get interface GUID (first 16 bytes of WLAN_INTERFACE_INFO)
                            Guid interfaceGuid = Marshal.PtrToStructure<Guid>(interfaceInfoPtr);

                            // Trigger an active scan to get fresh network list
                            System.Diagnostics.Debug.WriteLine($"[WifiScanner] Triggering scan for interface {interfaceGuid}");
                            uint scanResult = WlanScan(clientHandle, ref interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                            if (scanResult == 0)
                            {
                                // Wait a moment for scan to complete
                                System.Threading.Thread.Sleep(3000);
                                System.Diagnostics.Debug.WriteLine($"[WifiScanner] Scan completed, retrieving results");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[WifiScanner] Scan failed with code {scanResult}, using cached results");
                            }

                            // Get BSS list
                            IntPtr bssListPtr = IntPtr.Zero;
                            result = WlanGetNetworkBssList(clientHandle, ref interfaceGuid, IntPtr.Zero, DOT11_BSS_TYPE_ANY, false, IntPtr.Zero, out bssListPtr);

                            if (result == 0 && bssListPtr != IntPtr.Zero)
                            {
                                try
                                {
                                    // WLAN_BSS_LIST structure: dwTotalSize (4 bytes), dwNumberOfItems (4 bytes), then entries
                                    uint dwTotalSize = (uint)Marshal.ReadInt32(bssListPtr);
                                    uint dwNumberOfItems = (uint)Marshal.ReadInt32(new IntPtr(bssListPtr.ToInt64() + 4));

                                    System.Diagnostics.Debug.WriteLine($"[WifiScanner] Found {dwNumberOfItems} networks, total size: {dwTotalSize} bytes");

                                    // Sanity check
                                    if (dwNumberOfItems > 100 || dwNumberOfItems == 0 || dwTotalSize < 100)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[WifiScanner] Suspicious values or no networks found, using netsh fallback");
                                        return ScanWifiNetworksViaNetsh();
                                    }

                                    IntPtr bssEntryPtr = new IntPtr(bssListPtr.ToInt64() + 8);

                                    for (uint j = 0; j < dwNumberOfItems; j++)
                                    {
                                        try
                                        {
                                            // Use Marshal.PtrToStructure to read the entry (360 bytes fixed size)
                                            WLAN_BSS_ENTRY entry = Marshal.PtrToStructure<WLAN_BSS_ENTRY>(bssEntryPtr);

                                            // Extract SSID directly from structure fields
                                            string ssid = "";
                                            if (entry.uSSIDLength > 0 && entry.uSSIDLength <= 32)
                                            {
                                                ssid = System.Text.Encoding.UTF8.GetString(entry.ucSSID, 0, (int)entry.uSSIDLength);
                                                if (string.IsNullOrWhiteSpace(ssid))
                                                    ssid = "(Hidden Network)";
                                            }
                                            else if (entry.uSSIDLength == 0)
                                            {
                                                ssid = "(Hidden Network)";
                                            }
                                            else
                                            {
                                                System.Diagnostics.Debug.WriteLine($"[WifiScanner] Entry {j}: Invalid SSID length={entry.uSSIDLength}");
                                                ssid = "(Invalid SSID)";
                                            }

                                            // Extract BSSID and RSSI from the structure
                                            string bssidStr = BitConverter.ToString(entry.dot11BssId).Replace("-", ":");
                                            int rssi = entry.lRssi;

                                            // Only add if RSSI is reasonable (-100 to 0 dBm)
                                            if (rssi >= -100 && rssi <= 0 && entry.uSSIDLength <= 32)
                                            {
                                                var network = new WifiNetwork(ssid, bssidStr, rssi)
                                                {
                                                    // Network Type
                                                    NetworkType = GetNetworkType(entry.dot11BssType),

                                                    // PHY Type (802.11a/b/g/n/ac/ax)
                                                    PhyType = GetPhyType(entry.dot11BssPhyType),

                                                    // Link Quality (0-100)
                                                    LinkQuality = entry.uLinkQuality,

                                                    // Regulatory Domain
                                                    InRegDomain = entry.bInRegDomain != 0,

                                                    // Beacon Interval (typically 100ms)
                                                    BeaconInterval = entry.usBeaconPeriod,

                                                    // Frequency and Channel
                                                    Frequency = entry.ulChCenterFrequency,
                                                    Channel = FrequencyToChannel(entry.ulChCenterFrequency),

                                                    // Capability Information
                                                    Capabilities = entry.usCapabilityInformation,
                                                    IsSecured = (entry.usCapabilityInformation & 0x0010) != 0, // Privacy bit

                                                    // Supported Rates
                                                    SupportedRates = GetSupportedRates(entry.wlanRateSet),

                                                    // Timestamp
                                                    LastSeen = DateTime.Now,

                                                    // Physical ID
                                                    PhyId = entry.uPhyId
                                                };

                                                networks.Add(network);
                                                System.Diagnostics.Debug.WriteLine($"[WifiScanner] Entry {j}: {ssid}, BSSID={bssidStr}, RSSI={rssi}");
                                            }

                                            // Move to next entry using FIXED 360 byte size (like Vistumbler)
                                            bssEntryPtr = new IntPtr(bssEntryPtr.ToInt64() + 360);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[WifiScanner] Error processing entry {j}: {ex.Message}");
                                            break;
                                        }
                                    }

                                    System.Diagnostics.Debug.WriteLine($"[WifiScanner] Successfully added {networks.Count} networks");
                                }
                                finally
                                {
                                    WlanFreeMemory(bssListPtr);
                                }
                            }
                        }
                    }
                    finally
                    {
                        WlanFreeMemory(interfaceListPtr);
                    }
                }
                finally
                {
                    WlanCloseHandle(clientHandle, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WifiScanner] Error in ScanWifiNetworks: {ex}");
                // Fallback to netsh
                return ScanWifiNetworksViaNetsh();
            }

            // Remove duplicates and sort by signal strength
            return networks
                .GroupBy(n => n.BSSID)
                .Select(g => g.First())
                .OrderByDescending(n => n.SignalStrengthDbm)
                .ToList();
        }

        private static List<WifiNetwork> ScanWifiNetworksViaNetsh()
        {
            System.Diagnostics.Debug.WriteLine("[WifiScanner] Falling back to netsh method...");
            var networks = new List<WifiNetwork>();

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show networks mode=bssid",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null)
                    return networks;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase))
                    {
                        var colonIndex = trimmed.IndexOf(':');
                        if (colonIndex > 0)
                        {
                            var name = trimmed.Substring(colonIndex + 1).Trim();
                            if (!string.IsNullOrWhiteSpace(name) && !networks.Any(n => n.SSID == name))
                            {
                                networks.Add(new WifiNetwork(name, "00:00:00:00:00:00", -50));
                                System.Diagnostics.Debug.WriteLine($"[WifiScanner] netsh: Added {name}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WifiScanner] netsh fallback error: {ex}");
            }

            return networks;
        }

        // Helper methods for interpreting WiFi data
        private static string GetNetworkType(uint bssType)
        {
            return bssType switch
            {
                1 => "Infrastructure",
                2 => "Independent (Ad-Hoc)",
                3 => "Any",
                _ => $"Unknown ({bssType})"
            };
        }

        private static string GetPhyType(uint phyType)
        {
            return phyType switch
            {
                1 => "802.11 FHSS",
                2 => "802.11 DSSS",
                3 => "802.11 IR",
                4 => "802.11a",
                5 => "802.11b",
                6 => "802.11g",
                7 => "802.11n",
                8 => "802.11ac",
                9 => "802.11ad",
                10 => "802.11ax (Wi-Fi 6)",
                11 => "802.11be (Wi-Fi 7)",
                _ => $"Unknown ({phyType})"
            };
        }

        private static uint FrequencyToChannel(uint frequency)
        {
            if (frequency == 0) return 0;

            // 2.4 GHz band (channels 1-14)
            if (frequency >= 2412 && frequency <= 2484)
            {
                if (frequency == 2484) return 14;
                return (frequency - 2412) / 5 + 1;
            }

            // 5 GHz band (channels 36-165)
            if (frequency >= 5160 && frequency <= 5885)
            {
                return (frequency - 5000) / 5;
            }

            // 6 GHz band (Wi-Fi 6E, channels 1-233)
            if (frequency >= 5955 && frequency <= 7115)
            {
                return (frequency - 5950) / 5;
            }

            return 0;
        }

        private static string[] GetSupportedRates(uint[] wlanRateSet)
        {
            if (wlanRateSet == null || wlanRateSet.Length == 0)
                return Array.Empty<string>();

            var rates = new List<string>();

            // First 16 entries in the rate set contain the actual rates
            // Format: rate in units of 0.5 Mbps (bit 15 indicates basic rate)
            for (int i = 0; i < Math.Min(16, wlanRateSet.Length); i++)
            {
                uint rate = wlanRateSet[i];
                if (rate == 0) break; // End of rate set

                // Extract rate value (lower 15 bits)
                uint rateValue = rate & 0x7FFF;
                if (rateValue > 0 && rateValue < 1000) // Sanity check
                {
                    double mbps = rateValue * 0.5;
                    bool isBasic = (rate & 0x8000) != 0;
                    rates.Add(isBasic ? $"{mbps} Mbps*" : $"{mbps} Mbps");
                }
            }

            return rates.ToArray();
        }
    }
}
