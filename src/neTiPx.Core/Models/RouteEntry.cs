using CommunityToolkit.Mvvm.ComponentModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Repräsentiert einen Routing-Eintrag
/// </summary>
public sealed partial class RouteEntry : ObservableObject
{
    [ObservableProperty]
    private string _destination = string.Empty;

    [ObservableProperty]
    private string _subnetMask = string.Empty;

    [ObservableProperty]
    private string _gateway = string.Empty;

    [ObservableProperty]
    private int _metric = 1;

    [ObservableProperty]
    private bool _isProfileMatch;

    [ObservableProperty]
    private bool _canDeleteFromSystem = true;

    [ObservableProperty]
    private bool _showDeleteButton;

    [ObservableProperty]
    private bool _showApplyButton;

    public string FormattedRoute => $"{Destination}/{SubnetMask} via {Gateway}";
}
