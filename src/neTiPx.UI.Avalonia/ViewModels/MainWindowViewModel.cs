using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neTiPx.UI.Avalonia.Services;
using neTiPx.UI.Avalonia.Views;
using neTiPx.UI.Avalonia.Views.Tools;

namespace neTiPx.UI.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly LanguageManager _lm = LanguageManager.Instance;

    [ObservableProperty]
    private string _currentPageName = "Adapters";

    [ObservableProperty]
    private Control? _currentPage;

    [ObservableProperty]
    private bool _isPaneOpen = true;

    [ObservableProperty]
    private NavigationItem? _selectedNavigationItem;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public ObservableCollection<NavigationItem> FooterNavigationItems { get; } = new();

    public MainWindowViewModel()
    {
        _lm.LanguageChanged += (_, _) => RebuildNavigationItems();
        RebuildNavigationItems();
        SelectedNavigationItem = NavigationItems[0];
        UpdateCurrentPage();
    }

    private void RebuildNavigationItems()
    {
        var current = CurrentPageName;

        NavigationItems.Clear();
        NavigationItems.Add(new NavigationItem { Name = "Adapters", DisplayName = _lm.Lang("NAV_ADAPTERS"), Icon = "🔌" });
        NavigationItems.Add(new NavigationItem { Name = "IpConfig", DisplayName = _lm.Lang("NAV_IPCONFIG"), Icon = "⚙️" });
        NavigationItems.Add(new NavigationItem { Name = "Routes", DisplayName = _lm.Lang("TOOLS_ROUTES"), Icon = "🗺️" });
        NavigationItems.Add(new NavigationItem { Name = "UncPath", DisplayName = _lm.Lang("TOOLS_UNC_PATH"), Icon = "🗂️" });
        NavigationItems.Add(new NavigationItem { Name = "Tools", DisplayName = _lm.Lang("NAV_TOOLS"), Icon = "🔧" });

        FooterNavigationItems.Clear();
        FooterNavigationItems.Add(new NavigationItem { Name = "Info", DisplayName = _lm.Lang("NAV_INFO"), Icon = "ℹ️" });
        FooterNavigationItems.Add(new NavigationItem { Name = "Settings", DisplayName = _lm.Lang("NAV_SETTINGS"), Icon = "⚙️" });

        var selected = NavigationItems.FirstOrDefault(item => item.Name == current)
                       ?? FooterNavigationItems.FirstOrDefault(item => item.Name == current)
                       ?? NavigationItems.FirstOrDefault();

        if (selected != null)
        {
            SelectedNavigationItem = selected;
        }
    }

    [RelayCommand]
    private void NavigateTo(string pageName)
    {
        CurrentPageName = pageName;
        
        // Update SelectedNavigationItem to follow the current page
        var matchingItem = NavigationItems.FirstOrDefault(item => item.Name == pageName);
        if (matchingItem == null)
        {
            matchingItem = FooterNavigationItems.FirstOrDefault(item => item.Name == pageName);
        }
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
