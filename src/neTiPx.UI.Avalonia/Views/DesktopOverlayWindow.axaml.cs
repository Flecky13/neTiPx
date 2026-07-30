using System;
using Avalonia.Controls;
using Avalonia.Input;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class DesktopOverlayWindow : Window
{
    public DesktopOverlayWindow()
        : this(new DesktopOverlayViewModel())
    {
    }

    public DesktopOverlayWindow(DesktopOverlayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public DesktopOverlayViewModel ViewModel { get; }

    private void OverlayRoot_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!ViewModel.IsInteractive || !ViewModel.IsFreeMoveMode)
        {
            return;
        }

        try
        {
            BeginMoveDrag(e);
        }
        catch
        {
            // Ignore drag operation errors.
        }
    }
}
