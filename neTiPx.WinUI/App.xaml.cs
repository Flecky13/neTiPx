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
            var appWindow = WindowHelper.GetAppWindow(MainWindow);
            var minWidth = 1280;// Minimale Breite in Pixeln
            var minHeight = 920; // Minimale Höhe in Pixeln
            appWindow.ResizeClient(new Windows.Graphics.SizeInt32(minWidth, minHeight));

            ThemeService.ApplyTheme(rootFrame);

            _ = rootFrame.Navigate(typeof(Views.MainPage), e.Arguments);
            MainWindow.Activate();
            WindowHelper.Hide(MainWindow);

            _trayService = new TrayService(HoverWindow);

            // Enforce minimum size when user tries to resize
            appWindow.Changed += (sender, args) =>
            {
                var size = appWindow.Size;
                if (size.Width < minWidth || size.Height < minHeight)
                {
                    appWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                        Math.Max(size.Width, minWidth),
                        Math.Max(size.Height, minHeight)));
                }
            };

            // Prevent window close - minimize to tray instead
            appWindow.Closing += (sender, args) =>
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
