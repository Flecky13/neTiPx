using Avalonia.Controls;
using Avalonia.Interactivity;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class IpConfigPage : UserControl
{
    public IpConfigViewModel ViewModel { get; }
    
    public IpConfigPage()
    {
        ViewModel = new IpConfigViewModel();
        DataContext = ViewModel;
        
        InitializeComponent();
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.OnPageLoaded();
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.OnPageUnloaded();
    }
}
