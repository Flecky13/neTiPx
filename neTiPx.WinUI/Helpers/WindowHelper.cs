using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace neTiPx.WinUI.Helpers
{
    public static class WindowHelper
    {
        private const int SwHide = 0;
        private const int SwShownormal = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GwlExstyle = -20;
        private const int WsExNoactivate = 0x08000000;
        private const int WsExToolwindow = 0x00000080;

        public static IntPtr GetWindowHandle(Window window)
        {
            return WinRT.Interop.WindowNative.GetWindowHandle(window);
        }

        public static AppWindow GetAppWindow(Window window)
        {
            var hwnd = GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        public static void Show(Window window)
        {
            var handle = GetWindowHandle(window);
            ShowWindow(handle, SwShownormal);
            SetForegroundWindow(handle);
        }

        public static void Hide(Window window)
        {
            var handle = GetWindowHandle(window);
            ShowWindow(handle, SwHide);
        }

        public static PointInt32 GetCursorPoint()
        {
            if (!GetCursorPos(out var pt))
            {
                return new PointInt32(0, 0);
            }

            return new PointInt32(pt.X, pt.Y);
        }

        public static void PositionHoverWindow(Window window, int offsetY, int rightPadding)
        {
            var appWindow = GetAppWindow(window);
            var cursor = GetCursorPoint();
            var displayArea = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;

            var width = appWindow.Size.Width;
            var height = appWindow.Size.Height;

            int left = workArea.X + workArea.Width - width - rightPadding;
            int top = cursor.Y - height - offsetY;

            int minTop = workArea.Y + 10;
            int maxTop = workArea.Y + workArea.Height - height - 10;
            if (top < minTop) top = minTop;
            if (top > maxTop) top = maxTop;

            appWindow.Move(new PointInt32(left, top));
        }
    }
}
