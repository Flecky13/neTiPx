using Avalonia.Controls;
using neTiPx.UI.Avalonia.ViewModels;
using System.ComponentModel;

namespace neTiPx.UI.Avalonia.Views;

public partial class MainWindow : Window
{
    private static bool _allowCloseForExit;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
        
        Closing += OnWindowClosing;
    }

    public static void AllowCloseForExit()
    {
        _allowCloseForExit = true;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowCloseForExit)
        {
            return;
        }

        // Always minimize to tray instead of closing
        // User can exit from tray menu
        e.Cancel = true;
        Hide();
    }
}
