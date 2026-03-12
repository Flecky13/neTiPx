using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.ViewModels;
using Windows.Graphics;

namespace neTiPx.Views
{
    public sealed class RouteConfigWindow : Window
    {
        private readonly IpProfile _profile;
        private readonly IpConfigViewModel _viewModel;
        private readonly RouteConfigDialogContent _content;

        public RouteConfigWindow(IpProfile profile, IpConfigViewModel viewModel)
        {
            _profile = profile;
            _viewModel = viewModel;
            _content = new RouteConfigDialogContent(profile.Routes);

            Title = "Routen konfigurieren";
            Content = CreateLayout();

            ConfigureWindow();
        }

        private UIElement CreateLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentBorder = new Border
            {
                Padding = new Thickness(16, 16, 16, 8),
                Child = _content
            };
            Grid.SetRow(contentBorder, 0);
            root.Children.Add(contentBorder);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };

            var cancelButton = new Button
            {
                Content = "Abbrechen",
                MinWidth = 120
            };
            cancelButton.Click += (_, _) => Close();

            var applyButton = new Button
            {
                Content = "Übernehmen",
                MinWidth = 120
            };
            applyButton.Click += ApplyButton_Click;

            buttonPanel.Children.Add(cancelButton);
            buttonPanel.Children.Add(applyButton);

            Grid.SetRow(buttonPanel, 1);
            root.Children.Add(buttonPanel);

            return root;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var routes = _content.GetSanitizedRoutes();

            _profile.Routes.Clear();
            foreach (var route in routes)
            {
                _profile.Routes.Add(route);
            }

            _viewModel.RevalidateProfile();
            Close();
        }

        private void ConfigureWindow()
        {
            var appWindow = WindowHelper.GetAppWindow(this);
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = false;
            }

            const int width = 1400;
            const int height = 620;
            appWindow.Resize(new SizeInt32(width, height));

            var mainWindow = WindowHelper.GetAppWindow(App.MainWindow);
            var x = mainWindow.Position.X + Math.Max(40, (mainWindow.Size.Width - width) / 2);
            var y = mainWindow.Position.Y + Math.Max(40, (mainWindow.Size.Height - height) / 2);
            appWindow.Move(new PointInt32(x, y));
        }
    }
}
