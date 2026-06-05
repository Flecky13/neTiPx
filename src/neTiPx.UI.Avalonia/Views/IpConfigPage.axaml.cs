using Avalonia.Controls;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class IpConfigPage : UserControl
{
    public IpConfigPage()
    {
        InitializeComponent();
        DataContext = new IpConfigViewModel();
    }
}
