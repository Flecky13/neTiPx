using System;
using Avalonia.Controls;
using Avalonia.Input;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views;

public partial class DesktopOverlaySettingsWindow : Window
{
    public DesktopOverlaySettingsWindow()
    {
        InitializeComponent();
        Closed += (_, _) =>
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.DesktopOverlayHoverInteractive = false;
            }
        };
    }

    private async void OverlayItemDragStart_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        if (sender is not Control { DataContext: DesktopOverlayItemSettingViewModel item })
        {
            return;
        }

        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateText(item.DragId));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
    }

    private void OverlayItemDragOver(object? sender, DragEventArgs e)
    {
        var sourceKey = e.DataTransfer.TryGetText();
        if (!string.IsNullOrWhiteSpace(sourceKey))
        {
            e.DragEffects = DragDropEffects.Move;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void ServiceSelector_OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is Control { DataContext: DesktopOverlayItemSettingViewModel item })
        {
            item.IsServiceDropDownOpen = true;
        }
    }

    private void OverlayItemDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        if (sender is not Control { DataContext: DesktopOverlayItemSettingViewModel targetItem })
        {
            return;
        }

        var sourceKey = e.DataTransfer.TryGetText();
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return;
        }

        viewModel.MoveDesktopOverlayItem(sourceKey, targetItem.DragId);
    }
}
