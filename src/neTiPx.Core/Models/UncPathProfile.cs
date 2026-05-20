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

/// <summary>
/// Einzelner UNC-Pfad Eintrag
/// </summary>
public sealed partial class UncPathEntry : ObservableObject
{
    [ObservableProperty]
    private string _uncPath = string.Empty;

    [ObservableProperty]
    private string _driveLetter = string.Empty;

    [ObservableProperty]
    private bool _isPersistent;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsValid => !string.IsNullOrWhiteSpace(UncPath) 
        && UncPath.StartsWith(@"\\");
}
