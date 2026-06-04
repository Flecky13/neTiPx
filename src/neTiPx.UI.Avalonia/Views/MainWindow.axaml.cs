using Avalonia.Controls;
using neTiPx.UI.Avalonia.ViewModels;
using System.ComponentModel;

namespace neTiPx.UI.Avalonia.Views;

public partial class MainWindow : Window
{

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Always minimize to tray instead of closing
        // User can exit from tray menu
        e.Cancel = true;
        Hide();
    }
}
