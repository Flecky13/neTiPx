using Avalonia.Controls;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class InfoPage : UserControl
{
    public InfoPage()
    {
        InitializeComponent();
        DataContext = new InfoViewModel();
    }
}
