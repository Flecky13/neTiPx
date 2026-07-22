using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public sealed partial class DesktopOverlayItemSettingViewModel : ObservableObject
{
    public DesktopOverlayItemSettingViewModel(string key, string displayName, bool isVisible, bool showLabel, bool showValue)
    {
        Key = key;
        _displayName = displayName;
        _isVisible = isVisible;
        _showLabel = showLabel;
        _showValue = showValue;
    }

    public string Key { get; }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private bool _showLabel;

    [ObservableProperty]
    private bool _showValue;

    public DesktopOverlayItemSetting ToModel(int order)
    {
        return new DesktopOverlayItemSetting
        {
            Key = Key,
            IsVisible = IsVisible,
            ShowLabel = ShowLabel,
            ShowValue = ShowValue,
            Order = order
        };
    }
}
