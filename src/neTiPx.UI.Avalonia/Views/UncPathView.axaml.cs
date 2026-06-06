using Avalonia.Controls;
using Avalonia.Interactivity;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class UncPathView : UserControl
{
    public UncPathViewModel ViewModel { get; }

    public UncPathView()
    {
        ViewModel = new UncPathViewModel();
        DataContext = ViewModel;
        InitializeComponent();
        
        Loaded += OnLoaded;
    }
    
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewModel.LoadProfiles();
    }
}
