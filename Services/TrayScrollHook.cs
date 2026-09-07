using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Lumina.Services
{
    internal class TrayScrollHook : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private NativeMethods.LowLevelMouseProc? _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private IntPtr _trayHwnd = IntPtr.Zero;
        private uint _trayId = 1;

        public event Action<int>? Scrolled;

        private System.Drawing.Point _lastHoverPos;
        private DateTime _lastHoverTime = DateTime.MinValue;
        private bool _isHoveringIcon = false;

        public TrayScrollHook(NotifyIcon notifyIcon)
        {
            _notifyIcon = notifyIcon;
            _notifyIcon.MouseMove += (s, e) =>
            {
                _lastHoverPos = Cursor.Position;
                _isHoveringIcon = true;
                _lastHoverTime = DateTime.UtcNow;
            };

            _notifyIcon.MouseDown += (s, e) =>
            {
                _lastHoverPos = Cursor.Position;
                _isHoveringIcon = true;
                _lastHoverTime = DateTime.UtcNow;
            };

            ExtractNotifyIconIdentifiers();
            StartHook();
        }

        private void ExtractNotifyIconIdentifiers()
        {
            try
            {
                var windowField = typeof(NotifyIcon).GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
                if (windowField?.GetValue(_notifyIcon) is NativeWindow nativeWindow)
                {
                    _trayHwnd = nativeWindow.Handle;
                }

                var idField = typeof(NotifyIcon).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
                if (idField != null)
                {
                    object? val = idField.GetValue(_notifyIcon);
                    if (val != null)
                    {
                        _trayId = Convert.ToUInt32(val);
                    }
                }

                App.Log($"[TrayScrollHook] Extracted identifiers: hwnd=0x{_trayHwnd:X8}, id={_trayId}");
            }
            catch (Exception ex)
            {
                App.Log($"[TrayScrollHook] Failed to extract identifiers: {ex.Message}");
            }
        }

        public bool TryGetTrayIconRect(out NativeMethods.Rect rect)
        {
            rect = default;
            if (_trayHwnd == IntPtr.Zero)
            {
                ExtractNotifyIconIdentifiers();
            }

            if (_trayHwnd != IntPtr.Zero)
            {
                var nid = new NativeMethods.NOTIFYICONIDENTIFIER
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.NOTIFYICONIDENTIFIER>(),
                    hWnd = _trayHwnd,
                    uID = _trayId
                };

                int hr = NativeMethods.Shell_NotifyIconGetRect(ref nid, out rect);
                return hr == 0 && rect.Right > rect.Left && rect.Bottom > rect.Top;
            }

            return false;
        }

        private void StartHook()
        {
            _proc = HookCallback;
            using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);
            _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, moduleHandle, 0);
            App.Log($"[TrayScrollHook] Hook started. HookId={_hookId}");
        }

        private static string GetWindowClassName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return string.Empty;
            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static bool IsTaskbarOrTrayWindow(NativeMethods.POINT pt)
        {
            try
            {
                IntPtr hwnd = NativeMethods.WindowFromPoint(pt);
                if (hwnd == IntPtr.Zero) return false;

                string className = GetWindowClassName(hwnd);
                if (IsTrayClass(className)) return true;

                IntPtr rootHwnd = NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT);
                if (rootHwnd != IntPtr.Zero && rootHwnd != hwnd)
                {
                    string rootClass = GetWindowClassName(rootHwnd);
                    if (IsTrayClass(rootClass)) return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsTrayClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;
            return className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TrayNotifyWnd", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("NotifyIconOverflowWindow", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("SysPager", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("ToolbarWindow32", StringComparison.OrdinalIgnoreCase) ||
                   className.StartsWith("Windows.UI.Input.InputSite", StringComparison.OrdinalIgnoreCase);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

                if (msg == NativeMethods.WM_MOUSEMOVE)
                {
                    if (_isHoveringIcon)
                    {
                        int dx = Math.Abs(hookStruct.pt.x - _lastHoverPos.X);
                        int dy = Math.Abs(hookStruct.pt.y - _lastHoverPos.Y);
                        // If moved beyond tray icon bounding region (+ tolerance for high DPI), cursor left the icon
                        if (dx > 40 || dy > 40)
                        {
                            _isHoveringIcon = false;
                        }
                    }
                }
                else if (msg == NativeMethods.WM_MOUSEWHEEL)
                {
                    bool isOverTray = false;

                    // 1. Exact bounding box from Shell if available
                    if (TryGetTrayIconRect(out var rect))
                    {
                        if (hookStruct.pt.x >= rect.Left - 8 && hookStruct.pt.x <= rect.Right + 8 &&
                            hookStruct.pt.y >= rect.Top - 8 && hookStruct.pt.y <= rect.Bottom + 8)
                        {
                            isOverTray = true;
                        }
                    }

                    // 2. Hover proximity tracking: cursor hasn't moved away from where hover occurred
                    if (!isOverTray)
                    {
                        int dx = Math.Abs(hookStruct.pt.x - _lastHoverPos.X);
                        int dy = Math.Abs(hookStruct.pt.y - _lastHoverPos.Y);

                        if (_isHoveringIcon && dx <= 40 && dy <= 40)
                        {
                            isOverTray = true;
                        }
                        else if ((DateTime.UtcNow - _lastHoverTime).TotalSeconds < 10.0 && dx <= 48 && dy <= 48)
                        {
                            if (IsTaskbarOrTrayWindow(hookStruct.pt))
                            {
                                isOverTray = true;
                            }
                        }
                    }

                    if (isOverTray)
                    {
                        _lastHoverTime = DateTime.UtcNow;
                        _isHoveringIcon = true;
                        short delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                        App.Log($"[TrayScrollHook] Scrolled over tray icon: delta={delta}");
                        Scrolled?.Invoke(delta);
                        return (IntPtr)1; // Consume so taskbar doesn't scroll
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }
    }
}
