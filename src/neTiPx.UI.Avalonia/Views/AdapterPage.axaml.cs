using Avalonia.Controls;
using Avalonia.Interactivity;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class AdapterPage : UserControl
{
    public AdapterViewModel ViewModel { get; }
    
    public AdapterPage()
    {
        ViewModel = new AdapterViewModel();
        DataContext = ViewModel;
        
        InitializeComponent();
        
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.RegisterNetworkChangeEvents();
        ViewModel.StartConnectionMonitoring();
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.StopConnectionMonitoring();
        ViewModel.UnregisterNetworkChangeEvents();
    }
}
