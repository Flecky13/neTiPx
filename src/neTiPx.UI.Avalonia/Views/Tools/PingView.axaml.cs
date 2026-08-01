using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using neTiPx.UI.Avalonia.Models;
using neTiPx.UI.Avalonia.Services;
using neTiPx.UI.Avalonia.ViewModels;

namespace neTiPx.UI.Avalonia.Views.Tools;

public partial class PingView : UserControl
{
    private readonly PingToolViewModel _viewModel;
    private Window? _ownerWindow;

    public PingView()
    {
        InitializeComponent();

        var monitorService = App.ServiceProvider?.GetService<PingMonitorService>()
            ?? throw new InvalidOperationException("PingMonitorService is not available.");

        _viewModel = new PingToolViewModel(monitorService);
        DataContext = _viewModel;

        Loaded += (_, _) =>
        {
            _viewModel.OnPageLoaded();
            AttachUiStateObservers();
            UpdateUiContextState();
        };

        Unloaded += (_, _) =>
        {
            DetachUiStateObservers();
            _viewModel.OnPageUnloaded();
        };
    }

    private void AttachUiStateObservers()
    {
        PropertyChanged -= PingView_PropertyChanged;
        PropertyChanged += PingView_PropertyChanged;

        _ownerWindow = TopLevel.GetTopLevel(this) as Window;
        if (_ownerWindow != null)
        {
            _ownerWindow.PropertyChanged -= OwnerWindow_PropertyChanged;
            _ownerWindow.PropertyChanged += OwnerWindow_PropertyChanged;
        }
    }

    private void DetachUiStateObservers()
    {
        PropertyChanged -= PingView_PropertyChanged;

        if (_ownerWindow != null)
        {
            _ownerWindow.PropertyChanged -= OwnerWindow_PropertyChanged;
        }

        _ownerWindow = null;
    }

    private void PingView_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty)
        {
            UpdateUiContextState();
        }
    }

    private void OwnerWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty || e.Property == IsVisibleProperty)
        {
            UpdateUiContextState();
        }
    }

    private void UpdateUiContextState()
    {
        var isWindowUsable = _ownerWindow == null
            || (_ownerWindow.IsVisible && _ownerWindow.WindowState != WindowState.Minimized);

        var isActive = IsVisible && isWindowUsable;
        _viewModel.SetUiContextActive(isActive);
    }

    private void SaveInput_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.SaveInput();
    }

    private void CancelEdit_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.CancelEdit();
    }

    private void StartPause_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PingMonitorItem item })
        {
            if (item.IsRunning)
            {
                _viewModel.Pause(item);
            }
            else
            {
                _viewModel.Start(item);
            }
        }
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PingMonitorItem item })
        {
            _viewModel.TriggerNow(item);
        }
    }

    private void Edit_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PingMonitorItem item })
        {
            _viewModel.Edit(item);
        }
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: PingMonitorItem item })
        {
            _viewModel.Delete(item);
        }
    }
}
