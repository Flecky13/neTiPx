using CommunityToolkit.Mvvm.ComponentModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Repräsentiert einen IP-Adress-Eintrag mit Subnetzmaske
/// </summary>
public sealed partial class IpAddressEntry : ObservableObject
{
    [ObservableProperty]
    private string _ipAddress = string.Empty;

    [ObservableProperty]
    private string _subnetMask = string.Empty;

    [ObservableProperty]
    private bool _hasIpAddressError;

    [ObservableProperty]
    private bool _hasSubnetMaskError;

    [ObservableProperty]
    private bool _canRemove;

    public bool IsValid => !HasIpAddressError && !HasSubnetMaskError 
        && !string.IsNullOrWhiteSpace(IpAddress) 
        && !string.IsNullOrWhiteSpace(SubnetMask);
}
