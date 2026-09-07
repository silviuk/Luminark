using System;
using System.Reflection;
using System.Runtime.InteropServices;
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

        public TrayScrollHook(NotifyIcon notifyIcon)
        {
            _notifyIcon = notifyIcon;
            _notifyIcon.MouseMove += (s, e) =>
            {
                _lastHoverPos = System.Windows.Forms.Cursor.Position;
                _lastHoverTime = DateTime.UtcNow;
            };
            ExtractNotifyIconIdentifiers();
            StartHook();
        }

        private void ExtractNotifyIconIdentifiers()
        {
            try
            {
                var windowField = typeof(NotifyIcon).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance);
                if (windowField?.GetValue(_notifyIcon) is NativeWindow nativeWindow)
                {
                    _trayHwnd = nativeWindow.Handle;
                }

                var idField = typeof(NotifyIcon).GetField("id", BindingFlags.NonPublic | BindingFlags.Instance);
                if (idField != null)
                {
                    object? val = idField.GetValue(_notifyIcon);
                    if (val is int intId) _trayId = (uint)intId;
                }
            }
            catch { }
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
                return hr == 0; // S_OK
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

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (int)wParam == NativeMethods.WM_MOUSEWHEEL)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                bool isOverTray = false;

                // 1. Try exact bounding box if available from Shell
                if (TryGetTrayIconRect(out var rect) && rect.Right > rect.Left && rect.Bottom > rect.Top)
                {
                    if (hookStruct.pt.x >= rect.Left - 4 && hookStruct.pt.x <= rect.Right + 4 &&
                        hookStruct.pt.y >= rect.Top - 4 && hookStruct.pt.y <= rect.Bottom + 4)
                    {
                        isOverTray = true;
                    }
                }

                // 2. Fallback to hover proximity tracking (cursor is within 32px of recent hover point)
                if (!isOverTray && (DateTime.UtcNow - _lastHoverTime).TotalSeconds < 3.0)
                {
                    int dx = Math.Abs(hookStruct.pt.x - _lastHoverPos.X);
                    int dy = Math.Abs(hookStruct.pt.y - _lastHoverPos.Y);
                    if (dx <= 36 && dy <= 36)
                    {
                        isOverTray = true;
                    }
                }

                if (isOverTray)
                {
                    short delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                    App.Log($"[TrayScrollHook] Scrolled over tray icon: delta={delta}");
                    Scrolled?.Invoke(delta);
                    return (IntPtr)1; // Consume message so taskbar doesn't scroll
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
