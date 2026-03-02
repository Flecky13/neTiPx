using System;
using System.Runtime.InteropServices;
using System.Timers;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using neTiPx.Helpers;
using neTiPx.Views;
using WinRT.Interop;
using TimersTimer = System.Timers.Timer;

namespace neTiPx.Services
{
    public sealed class TrayService : IDisposable
    {
        private const int CallbackMessageId = 0x400 + 1;
        private const int MenuCommandOpen = 1001;
        private const int MenuCommandExit = 1002;

        private const int NifMessage = 0x00000001;
        private const int NifIcon = 0x00000002;
        private const int NifTip = 0x00000004;
        private const int NimAdd = 0x00000000;
        private const int NimDelete = 0x00000002;

        private const int WmMouseMove = 0x0200;
        private const int WmLButtonDblClk = 0x0203;
        private const int WmRButtonUp = 0x0205;

        private const uint TpmRightButton = 0x0002;
        private const uint TpmRetCmd = 0x0100;

        private const int GwlpWndproc = -4;

        private readonly TimersTimer _leaveTimer;
        private readonly HoverWindow _hoverWindow;
        private readonly DispatcherQueue _dispatcherQueue;

        private bool _isMouseOver;
        private Point _enterCursorPosition;
        private IntPtr _iconHandle = IntPtr.Zero;
        private IntPtr _windowHandle = IntPtr.Zero;
        private IntPtr _oldWndProc = IntPtr.Zero;
        private WndProcDelegate? _newWndProc;

        public TrayService(HoverWindow hoverWindow)
        {
            _hoverWindow = hoverWindow;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _leaveTimer = new TimersTimer(800) { AutoReset = false };
            _leaveTimer.Elapsed += LeaveTimer_Elapsed;

            InitializeTrayIcon();
        }

        public void Dispose()
        {
            RemoveTrayIcon();
            _leaveTimer.Dispose();
        }

        private void InitializeTrayIcon()
        {
            _windowHandle = WindowNative.GetWindowHandle(App.MainWindow);
            HookWndProc();

            _iconHandle = LoadTrayIcon();

            var data = new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                hWnd = _windowHandle,
                uID = 1,
                uFlags = NifMessage | NifIcon | NifTip,
                uCallbackMessage = CallbackMessageId,
                hIcon = _iconHandle,
                szTip = "neTiPx"
            };

            Shell_NotifyIcon(NimAdd, ref data);
        }

        private void RemoveTrayIcon()
        {
            if (_windowHandle != IntPtr.Zero)
            {
                var data = new NotifyIconData
                {
                    cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                    hWnd = _windowHandle,
                    uID = 1
                };

                Shell_NotifyIcon(NimDelete, ref data);
            }

            if (_iconHandle != IntPtr.Zero)
            {
                DestroyIcon(_iconHandle);
                _iconHandle = IntPtr.Zero;
            }

            UnhookWndProc();
        }

        private void HookWndProc()
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return;
            }

            _newWndProc = WndProc;
            _oldWndProc = SetWindowLongPtr(_windowHandle, GwlpWndproc, _newWndProc);
        }

        private void UnhookWndProc()
        {
            if (_windowHandle == IntPtr.Zero || _oldWndProc == IntPtr.Zero)
            {
                return;
            }

            SetWindowLongPtr(_windowHandle, GwlpWndproc, _oldWndProc);
            _oldWndProc = IntPtr.Zero;
            _newWndProc = null;
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == CallbackMessageId)
            {
                int mouseMessage = lParam.ToInt32();
                switch (mouseMessage)
                {
                    case WmMouseMove:
                        HandleMouseMove();
                        break;
                    case WmLButtonDblClk:
                        ShowMainWindow();
                        break;
                    case WmRButtonUp:
                        ShowContextMenu();
                        break;
                }
            }

            return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
        }

        private void HandleMouseMove()
        {
            if (!_isMouseOver)
            {
                _isMouseOver = true;
                _enterCursorPosition = GetCursorPosition();

                // Hover Window nur anzeigen, wenn es aktiviert ist
                var settingsService = new SettingsService();
                if (settingsService.GetHoverWindowEnabled())
                {
                    ShowHoverWindow();
                }
            }

            _leaveTimer.Stop();
            _leaveTimer.Start();
        }

        private void LeaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var current = GetCursorPosition();
            int dx = current.X - _enterCursorPosition.X;
            int dy = current.Y - _enterCursorPosition.Y;
            var distSq = dx * dx + dy * dy;
            const int leaveDistancePx = 16;

            if (distSq >= leaveDistancePx * leaveDistancePx)
            {
                _isMouseOver = false;
                HideHoverWindow();
            }
            else
            {
                _leaveTimer.Stop();
                _leaveTimer.Start();
            }
        }

        private void ShowHoverWindow()
        {
            _dispatcherQueue.TryEnqueue(async () =>
            {
                // Hole die Verzögerungs-Einstellung
                var settingsService = new SettingsService();
                int delayMs = settingsService.GetHoverWindowDelaySeconds() * 1000;

                // Warte die konfigutrierte Zeit
                await Task.Delay(delayMs);

                // Prüfe ob wir immer noch hovern (sicherheitshalber)
                if (_isMouseOver)
                {
                    await _hoverWindow.RefreshAsync();
                    WindowHelper.Show(_hoverWindow);
                    WindowHelper.PositionHoverWindow(_hoverWindow, 20, 10);
                }
            });
        }

        private void HideHoverWindow()
        {
            _dispatcherQueue.TryEnqueue(() => WindowHelper.Hide(_hoverWindow));
        }

        private void ShowMainWindow()
        {
            WindowHelper.Show(App.MainWindow);
        }

        private void ShowContextMenu()
        {
            var menu = CreatePopupMenu();
            AppendMenu(menu, 0, MenuCommandOpen, "Öffnen");
            AppendMenu(menu, 0x800, 0, null);
            AppendMenu(menu, 0, MenuCommandExit, "Beenden");

            var cursor = GetCursorPosition();
            SetForegroundWindow(_windowHandle);
            uint cmd = TrackPopupMenu(menu, TpmRightButton | TpmRetCmd, cursor.X, cursor.Y, 0, _windowHandle, IntPtr.Zero);

            if (cmd == MenuCommandOpen)
            {
                ShowMainWindow();
            }
            else if (cmd == MenuCommandExit)
            {
                Dispose();
                App.ExitApp();
            }

            DestroyMenu(menu);
        }

        private static IntPtr LoadTrayIcon()
        {
            var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "toolicon.ico");
            if (!System.IO.File.Exists(iconPath))
            {
                return IntPtr.Zero;
            }

            return LoadImage(IntPtr.Zero, iconPath, 1, 0, 0, 0x00000010);
        }

        private static Point GetCursorPosition()
        {
            GetCursorPos(out var pt);
            return pt;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NotifyIconData
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hInst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64Delegate(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern IntPtr SetWindowLong32Delegate(IntPtr hWnd, int nIndex, WndProcDelegate newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64Ptr(IntPtr hWnd, int nIndex, IntPtr newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern IntPtr SetWindowLong32Ptr(IntPtr hWnd, int nIndex, IntPtr newProc);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate newProc)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64Delegate(hWnd, nIndex, newProc) : SetWindowLong32Delegate(hWnd, nIndex, newProc);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64Ptr(hWnd, nIndex, newProc) : SetWindowLong32Ptr(hWnd, nIndex, newProc);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
