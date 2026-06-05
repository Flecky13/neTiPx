using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace neTiPx.Core.Models;

/// <summary>
/// UNC-Pfad Profil für Netzwerk-Freigaben
/// </summary>
public sealed partial class UncPathProfile : ObservableObject
{
    [ObservableProperty]
    private string _name = "UNC #1";

    [ObservableProperty]
    private bool _isDirty;

    public ObservableCollection<UncPathEntry> UncPaths { get; } = new();
}
