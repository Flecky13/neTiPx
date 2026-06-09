using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neTiPx.UI.Avalonia.Views;
using neTiPx.UI.Avalonia.Views.Tools;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPageName = "Adapters";

    [ObservableProperty]
    private Control? _currentPage;

    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new()
    {
        new NavigationItem { Name = "Adapters", DisplayName = "Adapter Infos", Icon = "🔌" },
        new NavigationItem { Name = "IpConfig", DisplayName = "IP Konfiguration", Icon = "⚙️" },
        new NavigationItem { Name = "Routes", DisplayName = "Routen", Icon = "🗺️" },
        new NavigationItem { Name = "UncPath", DisplayName = "UNC Pfade", Icon = "🗂️" },
        new NavigationItem { Name = "Tools", DisplayName = "Tools", Icon = "🔧" },
        new NavigationItem { Name = "Info", DisplayName = "Info", Icon = "ℹ️" },
        new NavigationItem { Name = "Settings", DisplayName = "Einstellungen", Icon = "⚙️" }
    };

    public MainWindowViewModel()
    {
        SelectedNavigationItem = NavigationItems[0];
        UpdateCurrentPage();
    }

    [RelayCommand]
    private void NavigateTo(string pageName)
    {
        CurrentPageName = pageName;
        
        // Update SelectedNavigationItem to follow the current page
        var matchingItem = NavigationItems.FirstOrDefault(item => item.Name == pageName);
        if (matchingItem != null)
        {
            SelectedNavigationItem = matchingItem;
        }
        
        UpdateCurrentPage();
    }

    [RelayCommand]
    private void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    private void UpdateCurrentPage()
    {
        CurrentPage = CurrentPageName switch
        {
            "Adapters" => new AdapterPage(),
            "IpConfig" => new IpConfigPage(),
            "Routes" => new RoutesView(),
            "UncPath" => new UncPathView(),
            "Tools" => new ToolsPage(),
            "Info" => new InfoPage(),
            "Settings" => new SettingsPage(),
            _ => new AdapterPage()
        };
    }
}

public class NavigationItem
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
