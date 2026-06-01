using Avalonia.Controls;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class SettingsPage : UserControl
{
    public SettingsViewModel ViewModel { get; }
    
    public SettingsPage()
    {
        ViewModel = new SettingsViewModel();
        DataContext = ViewModel;
        
        InitializeComponent();
    }
}
