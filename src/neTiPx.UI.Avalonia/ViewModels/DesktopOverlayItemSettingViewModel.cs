using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public sealed partial class DesktopOverlayItemSettingViewModel : ObservableObject
{
    public DesktopOverlayItemSettingViewModel(
        ObservableCollection<DesktopOverlayInfoOption> infoOptions,
        string key,
        bool showLabel,
        string? customText)
    {
        InfoOptions = infoOptions;
        _selectedInfoOption = infoOptions.FirstOrDefault(option => option.Key == key);
        _showLabel = showLabel;
        _customText = customText ?? string.Empty;
    }

    public ObservableCollection<DesktopOverlayInfoOption> InfoOptions { get; }

    [ObservableProperty]
    private DesktopOverlayInfoOption? _selectedInfoOption;

    [ObservableProperty]
    private bool _showLabel;

    [ObservableProperty]
    private string _customText;

    public string Key => SelectedInfoOption?.Key ?? string.Empty;

    public string DisplayName => SelectedInfoOption?.DisplayName ?? string.Empty;

    public bool IsFreeText => Key.Equals(DesktopOverlayInfoKeys.FreeText, System.StringComparison.OrdinalIgnoreCase);

    public bool ShowLabelSelector => !IsFreeText;

    partial void OnSelectedInfoOptionChanged(DesktopOverlayInfoOption? value)
    {
        OnPropertyChanged(nameof(Key));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsFreeText));
        OnPropertyChanged(nameof(ShowLabelSelector));
    }

    public DesktopOverlayItemSetting ToModel(int order)
    {
        return new DesktopOverlayItemSetting
        {
            Key = Key,
            ShowLabel = ShowLabel,
            CustomText = CustomText,
            Order = order
        };
    }
}

public sealed partial class DesktopOverlayInfoOption : ObservableObject
{
    public DesktopOverlayInfoOption(string key, string displayName)
    {
        Key = key;
        _displayName = displayName;
    }

    public string Key { get; }

    [ObservableProperty]
    private string _displayName;
}
