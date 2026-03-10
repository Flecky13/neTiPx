using System;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.ViewModels;
using Windows.Graphics;
using Windows.UI;

namespace neTiPx.Views
{
    public sealed class HoverWindow : Window
    {
        private HoverViewModel ViewModelInstance;
        private ScrollViewer? _scrollViewer;

        public HoverWindow()
        {
            ViewModelInstance = new HoverViewModel();
            CreateContent();
            ConfigureWindow();
            // Adjust height after content is created
            AdjustWindowHeight();
        }

        private void CreateContent()
        {
            var goldenrod = new SolidColorBrush(Color.FromArgb(255, 218, 165, 32));
            var papayaWhip = new SolidColorBrush(Color.FromArgb(255, 255, 239, 213));
            var black = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0));
            var white = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));

            // Main ScrollViewer for dynamic height
            _scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = white
            };

            // Main StackPanel
            var mainStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 0,
                Background = white
            };

            // === Public IP Section ===
            var ipGrid = new Grid
            {
                Background = papayaWhip,
                Padding = new Thickness(5),
                Margin = new Thickness(5)
            };
            ipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            ipGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var ipLabel = new TextBlock
            {
                Text = "Öffentliche IP:",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = black,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(ipLabel, 0);
            ipGrid.Children.Add(ipLabel);

            var ipValue = new TextBlock
            {
                FontSize = 13.5,
                Foreground = black,
                VerticalAlignment = VerticalAlignment.Center
            };
            ipValue.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath("PublicIp"),
                Source = ViewModelInstance
            });
            Grid.SetColumn(ipValue, 1);
            ipGrid.Children.Add(ipValue);

            mainStack.Children.Add(ipGrid);

            // === Separator ===
            var sep1 = new Border { Height = 5, Background = goldenrod, Margin = new Thickness(5, 0, 5, 5) };
            mainStack.Children.Add(sep1);

            // === NIC1 Section ===
            mainStack.Children.Add(CreateNicSection("Nic1", ViewModelInstance, papayaWhip, black, white));

            // === NIC2 Section (only if available) ===
            var nic2Container = new Grid { Background = white };
            var nic2Section = CreateNicSection("Nic2", ViewModelInstance, papayaWhip, black, white);
            nic2Container.Children.Add(nic2Section);
            // Bind visibility to HasNic2
            nic2Container.SetBinding(UIElement.VisibilityProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath("HasNic2"),
                Source = ViewModelInstance,
                Converter = new BooleanToVisibilityConverter()
            });
            mainStack.Children.Add(nic2Container);

            _scrollViewer.Content = mainStack;
            this.Content = _scrollViewer;
        }

        private StackPanel CreateNicSection(string nicPrefix, HoverViewModel viewModel,
            SolidColorBrush headerBg, SolidColorBrush textColor, SolidColorBrush bgColor)
        {
            var section = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 5,
                Padding = new Thickness(5),
                Background = bgColor,
                Margin = new Thickness(5, 0, 5, 5)
            };

            // Header
            var headerBorder = new Border
            {
                Background = headerBg,
                Padding = new Thickness(3),
                Margin = new Thickness(-5, -5, -5, 0)
            };
            var header = new TextBlock
            {
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = textColor
            };
            // Bind header text to adapter name
            string nameProperty = nicPrefix == "Nic1" ? "Nic1Name" : "Nic2Name";
            header.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath(nameProperty),
                Source = viewModel
            });
            headerBorder.Child = header;
            section.Children.Add(headerBorder);

            // IPv4 section
            section.Children.Add(CreateDetailGrid("IPv4:", $"{nicPrefix}Ipv4", textColor));
            section.Children.Add(CreateDetailGrid("Gateway4:", $"{nicPrefix}Gateway4", textColor));
            section.Children.Add(CreateDetailGrid("DNS4:", $"{nicPrefix}Dns4", textColor));

            // IPv6 section (with visibility binding)
            var ipv6Container = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 5
            };
            ipv6Container.Children.Add(CreateDetailGrid("IPv6:", $"{nicPrefix}Ipv6", textColor));
            ipv6Container.Children.Add(CreateDetailGrid("Gateway6:", $"{nicPrefix}Gateway6", textColor));
            ipv6Container.Children.Add(CreateDetailGrid("DNS6:", $"{nicPrefix}Dns6", textColor));

            // Bind IPv6 section visibility
            string hasIpv6Property = nicPrefix == "Nic1" ? "HasNic1Ipv6" : "HasNic2Ipv6";
            ipv6Container.SetBinding(UIElement.VisibilityProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath(hasIpv6Property),
                Source = ViewModelInstance,
                Converter = new BooleanToVisibilityConverter()
            });
            section.Children.Add(ipv6Container);

            return section;
        }

        private Grid CreateDetailGrid(string label, string propertyBinding, SolidColorBrush textColor)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = textColor,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                FontSize = 12,
                Foreground = textColor,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 5, 0)
            };
            valueBlock.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding
            {
                Path = new PropertyPath(propertyBinding),
                Source = ViewModelInstance
            });
            Grid.SetColumn(valueBlock, 1);
            Grid.SetRow(valueBlock, 0);
            grid.Children.Add(valueBlock);

            return grid;
        }

        public async Task RefreshAsync()
        {
            await ViewModelInstance.RefreshAsync();
            // Remeasure after refresh to adjust height dynamically
            AdjustWindowHeight();
        }

        private void AdjustWindowHeight()
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.UpdateLayout();
                _scrollViewer.Measure(new Windows.Foundation.Size(320, double.PositiveInfinity));
                double contentHeight = _scrollViewer.DesiredSize.Height;
                double finalHeight = Math.Min(contentHeight + 20, 600);
                var appWindow = WindowHelper.GetAppWindow(this);
                appWindow.Resize(new Windows.Graphics.SizeInt32(320, (int)finalHeight));
            }
        }

        private void ConfigureWindow()
        {
            var appWindow = WindowHelper.GetAppWindow(this);

            // Set initial size (will be adjusted based on content)
            appWindow.Resize(new Windows.Graphics.SizeInt32(320, 100));

            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }

            try
            {
                appWindow.IsShownInSwitchers = false;
            }
            catch
            {
            }

            WindowHelper.Hide(this);
        }

        // Helper converter class
        public sealed class BooleanToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
        {
            public object Convert(object value, System.Type targetType, object parameter, string language)
            {
                if (value is bool boolValue)
                    return boolValue ? Visibility.Visible : Visibility.Collapsed;
                return Visibility.Collapsed;
            }

            public object ConvertBack(object value, System.Type targetType, object parameter, string language)
            {
                if (value is Visibility visibility)
                    return visibility == Visibility.Visible;
                return false;
            }
        }
    }
}
