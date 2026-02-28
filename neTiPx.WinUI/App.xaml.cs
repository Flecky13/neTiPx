using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Windowing;
using neTiPx.WinUI.Helpers;
using neTiPx.WinUI.Services;
using neTiPx.WinUI.Views;

namespace neTiPx.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public static Window MainWindow { get; private set; } = null!;
        public static HoverWindow HoverWindow { get; private set; } = null!;
        public static ThemeService ThemeService { get; } = new ThemeService();

        private TrayService? _trayService;
        private static int MIN_WIDTH = 1280;
        private const int MIN_HEIGHT = 940;
        private const int MIN_WIDTH_PANE_OPEN = 1280;
        private const int MIN_WIDTH_PANE_CLOSED = 1008;
        private static AppWindow? _appWindow;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            MainWindow = new Window();
            HoverWindow = new HoverWindow();

            var rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            MainWindow.Content = rootFrame;

            MainWindow.Title = "neTiPx";
            MainWindow.ExtendsContentIntoTitleBar = true;

            SetWindowIcon(MainWindow);
            SetWindowIcon(HoverWindow);

            // Set minimum window size and enforce it
            _appWindow = WindowHelper.GetAppWindow(MainWindow);

            // Set minimum size using OverlappedPresenter
            if (_appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }

            // Set initial and minimum size
            _appWindow.Resize(new Windows.Graphics.SizeInt32(MIN_WIDTH, MIN_HEIGHT));
            ThemeService.ApplyTheme(rootFrame);

            _ = rootFrame.Navigate(typeof(Views.MainPage), e.Arguments);
            MainWindow.Activate();
            WindowHelper.Hide(MainWindow);

            _trayService = new TrayService(HoverWindow);

            // Enforce minimum size when user tries to resize
            _appWindow.Changed += (sender, args) =>
            {
                if (args.DidSizeChange)
                {
                    var size = _appWindow.Size;
                    if (size.Width < MIN_WIDTH || size.Height < MIN_HEIGHT)
                    {
                        _appWindow.Resize(new Windows.Graphics.SizeInt32(
                            Math.Max(size.Width, MIN_WIDTH),
                            Math.Max(size.Height, MIN_HEIGHT)));
                    }
                }
            };

            // Prevent window close - minimize to tray instead
            _appWindow.Closing += (sender, args) =>
            {
                args.Cancel = true;
                WindowHelper.Hide(MainWindow);
            };
        }

        private static void SetWindowIcon(Window window)
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "toolicon.ico");
            if (!File.Exists(iconPath))
            {
                return;
            }

            var appWindow = WindowHelper.GetAppWindow(window);
            appWindow.SetIcon(iconPath);
        }

        public static void ExitApp()
        {
            Current.Exit();
        }

        public static void UpdateMinWidth(bool isPaneOpen)
        {
            MIN_WIDTH = isPaneOpen ? MIN_WIDTH_PANE_OPEN : MIN_WIDTH_PANE_CLOSED;

            if (_appWindow != null)
            {
                var currentSize = _appWindow.Size;
                if (currentSize.Width < MIN_WIDTH)
                {
                    _appWindow.Resize(new Windows.Graphics.SizeInt32(MIN_WIDTH, currentSize.Height));
                }
            }
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
