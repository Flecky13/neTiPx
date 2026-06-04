using System.Threading.Tasks;
using Avalonia.Controls;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class HoverWindow : Window
{
    private readonly HoverViewModel _viewModel;

    public HoverWindow()
    {
        InitializeComponent();
        _viewModel = new HoverViewModel();
        DataContext = _viewModel;
    }

    public async Task RefreshAsync()
    {
        await _viewModel.RefreshAsync();
    }
}
