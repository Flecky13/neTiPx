using Avalonia.Controls;
using neTiPx.UI.Avalonia.ViewModels;
using System.ComponentModel;

namespace neTiPx.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private static bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        Closing += OnWindowClosing;
    }

    public static void AllowCloseOnce()
    {
        _allowClose = true;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            _allowClose = false;
            return;
        }

        // Always minimize to tray instead of closing
        // User can exit from tray menu
        e.Cancel = true;
        Hide();
    }
}
