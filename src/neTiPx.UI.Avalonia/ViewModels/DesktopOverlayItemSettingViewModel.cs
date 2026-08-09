using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

public sealed partial class DesktopOverlayItemSettingViewModel : ObservableObject
{
    public DesktopOverlayItemSettingViewModel(
        ObservableCollection<DesktopOverlayInfoOption> infoOptions,
        ObservableCollection<DesktopOverlayAdapterOption> adapterOptions,
        ObservableCollection<SystemServiceInfo> services,
        string key,
        bool showLabel,
        string? customText,
        string? adapterName = null,
        string? serviceName = null)
    {
        InfoOptions = infoOptions;
        AdapterOptions = adapterOptions;
        Services = services;
        _selectedInfoOption = infoOptions.FirstOrDefault(option => option.Key == key);
        _showLabel = showLabel;
        _customText = customText ?? string.Empty;
        _adapterName = adapterName ?? string.Empty;
        _serviceName = serviceName ?? string.Empty;
        ResolveSelectedAdapter();
        ResolveSelectedService();
    }

    public ObservableCollection<DesktopOverlayInfoOption> InfoOptions { get; }
    public ObservableCollection<DesktopOverlayAdapterOption> AdapterOptions { get; }
    public ObservableCollection<SystemServiceInfo> Services { get; }

    [ObservableProperty]
    private DesktopOverlayInfoOption? _selectedInfoOption;

    [ObservableProperty]
    private bool _showLabel;

    [ObservableProperty]
    private string _customText;

    [ObservableProperty]
    private string _adapterName;

    [ObservableProperty]
    private DesktopOverlayAdapterOption? _selectedAdapterOption;

    [ObservableProperty]
    private string _serviceName;

    [ObservableProperty]
    private SystemServiceInfo? _selectedService;

    [ObservableProperty]
    private bool _isServiceDropDownOpen;

    public string DragId { get; } = System.Guid.NewGuid().ToString("N");

    public string Key => SelectedInfoOption?.Key ?? string.Empty;

    public string DisplayName => SelectedInfoOption?.DisplayName ?? string.Empty;

    public bool IsFreeText => Key.Equals(DesktopOverlayInfoKeys.FreeText, System.StringComparison.OrdinalIgnoreCase);

    public bool ShowLabelSelector => !IsFreeText;

    public bool IsServiceStatus => Key.Equals(DesktopOverlayInfoKeys.ServiceStatus, System.StringComparison.OrdinalIgnoreCase);

    public bool IsAdapterSelectable => DesktopOverlayInfoKeys.SupportsAdapterSelection(Key);

    partial void OnSelectedInfoOptionChanged(DesktopOverlayInfoOption? value)
    {
        OnPropertyChanged(nameof(Key));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(IsFreeText));
        OnPropertyChanged(nameof(ShowLabelSelector));
        OnPropertyChanged(nameof(IsServiceStatus));
        OnPropertyChanged(nameof(IsAdapterSelectable));
    }

    partial void OnSelectedAdapterOptionChanged(DesktopOverlayAdapterOption? value)
    {
        var adapterName = value?.AdapterName ?? string.Empty;
        if (!string.Equals(AdapterName, adapterName, System.StringComparison.Ordinal))
        {
            AdapterName = adapterName;
        }
    }

    partial void OnSelectedServiceChanged(SystemServiceInfo? value)
    {
        if (value != null && !string.Equals(ServiceName, value.Name, System.StringComparison.OrdinalIgnoreCase))
        {
            ServiceName = value.Name;
        }

    }

    partial void OnServiceNameChanged(string value) => ResolveSelectedService();

    partial void OnAdapterNameChanged(string value) => ResolveSelectedAdapter();

    public void RefreshServices() => ResolveSelectedService();

    public void RefreshAdapters() => ResolveSelectedAdapter();

    public void ResolveSelectedAdapter()
    {
        var selected = AdapterOptions.FirstOrDefault(option =>
            option.AdapterName.Equals(AdapterName, System.StringComparison.OrdinalIgnoreCase));
        selected ??= AdapterOptions.FirstOrDefault(option => string.IsNullOrWhiteSpace(option.AdapterName));
        if (!ReferenceEquals(SelectedAdapterOption, selected))
        {
            SelectedAdapterOption = selected;
        }
    }

    public void ResolveSelectedService()
    {
        var selected = Services.FirstOrDefault(service =>
            service.Name.Equals(ServiceName, System.StringComparison.OrdinalIgnoreCase));
        if (!ReferenceEquals(SelectedService, selected))
        {
            SelectedService = selected;
        }
    }

    public DesktopOverlayItemSetting ToModel(int order)
    {
        return new DesktopOverlayItemSetting
        {
            Key = Key,
            ShowLabel = ShowLabel,
            CustomText = CustomText,
            AdapterName = AdapterName,
            ServiceName = ServiceName,
            Order = order
        };
    }
}

public sealed partial class DesktopOverlayAdapterOption : ObservableObject
{
    public DesktopOverlayAdapterOption(string adapterName, string displayName)
    {
        AdapterName = adapterName;
        _displayName = displayName;
    }

    public string AdapterName { get; }

    [ObservableProperty]
    private string _displayName;
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
