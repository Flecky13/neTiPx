using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using neTiPx.UI.Avalonia.ViewModels;
using neTiPx.UI.Avalonia.Views;

namespace neTiPx.UI.Avalonia.Services;

public sealed class DesktopOverlayController : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly DesktopOverlayViewModel _viewModel;
    private readonly DesktopOverlayWindow _window;

    private bool _disposed;
    private bool _isApplyingSettings;

    public DesktopOverlayController()
    {
        _settingsService = new SettingsService();
        _viewModel = new DesktopOverlayViewModel();
        _window = new DesktopOverlayWindow(_viewModel);

        SettingsService.UserSettingsChanged += OnUserSettingsChanged;
    }

    public async Task InitializeAsync()
    {
        await ApplySettingsAsync(forceRefresh: true);
    }

    private async void OnUserSettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        await ApplySettingsAsync(forceRefresh: false);
    }

    private async Task ApplySettingsAsync(bool forceRefresh)
    {
        if (_isApplyingSettings || _disposed)
        {
            return;
        }

        _isApplyingSettings = true;

        try
        {
            var settings = DesktopOverlaySettingsModel.Normalize(_settingsService.GetDesktopOverlaySettings(forceReload: true));

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Moving is deliberately only persisted when temporary interaction is turned off.
                // Applying the unchecked setting must therefore capture the window's current location
                // before ApplyWindowPosition could move it back to an older saved location.
                if (_viewModel.IsFreeMoveMode && _viewModel.IsInteractive
                    && string.Equals(settings.PositionMode, DesktopOverlayPositionModes.Free, StringComparison.OrdinalIgnoreCase)
                    && !settings.HoverInteractive)
                {
                    settings.FreeX = Math.Max(0, _window.Position.X);
                    settings.FreeY = Math.Max(0, _window.Position.Y);
                    _settingsService.SetDesktopOverlaySettings(settings);
                }

                _viewModel.ApplySettings(settings);

                _window.Width = settings.Width;
                _window.Height = settings.Height;
                _window.ShowActivated = false;
                _window.ShowInTaskbar = false;
                _window.Topmost = false;

                ApplyWindowPosition(settings);

                if (settings.Enabled)
                {
                    if (!_window.IsVisible)
                    {
                        _window.Show();
                    }

                    _viewModel.Start();
                }
                else
                {
                    _viewModel.Stop();
                    if (_window.IsVisible)
                    {
                        _window.Hide();
                    }
                }
            }, DispatcherPriority.Send);

            if (settings.Enabled)
            {
                if (forceRefresh)
                {
                    await _viewModel.RefreshNowAsync();
                }
                else
                {
                    await _viewModel.RefreshNowAsync();
                }
            }
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void ApplyWindowPosition(DesktopOverlaySettingsModel settings)
    {
        var screen = _window.Screens?.Primary;
        if (screen == null)
        {
            return;
        }

        var workArea = screen.WorkingArea;

        if (string.Equals(settings.PositionMode, DesktopOverlayPositionModes.Free, StringComparison.OrdinalIgnoreCase))
        {
            _window.Position = new PixelPoint(settings.FreeX, settings.FreeY);
            return;
        }

        var width = (int)Math.Round(_window.Width);
        var height = (int)Math.Round(_window.Height);

        var x = workArea.X + settings.OffsetX;
        var y = workArea.Y + settings.OffsetY;

        if (string.Equals(settings.PositionMode, DesktopOverlayPositionModes.TopRight, StringComparison.OrdinalIgnoreCase)
            || string.Equals(settings.PositionMode, DesktopOverlayPositionModes.BottomRight, StringComparison.OrdinalIgnoreCase))
        {
            x = workArea.Right - width - settings.OffsetX;
        }

        if (string.Equals(settings.PositionMode, DesktopOverlayPositionModes.BottomLeft, StringComparison.OrdinalIgnoreCase)
            || string.Equals(settings.PositionMode, DesktopOverlayPositionModes.BottomRight, StringComparison.OrdinalIgnoreCase))
        {
            y = workArea.Bottom - height - settings.OffsetY;
        }

        _window.Position = new PixelPoint(Math.Max(workArea.X, x), Math.Max(workArea.Y, y));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SettingsService.UserSettingsChanged -= OnUserSettingsChanged;
        _viewModel.Stop();

        if (_window.IsVisible)
        {
            _window.Hide();
        }

        _window.Close();
    }
}
