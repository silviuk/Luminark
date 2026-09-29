using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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
        private uint _hookThreadId = 0;
        private Thread? _hookThread;
        private readonly ManualResetEventSlim _startedEvent = new(false);
        private volatile bool _disposed = false;
        private volatile bool _isEnabled = true;

        public event Action<int>? Scrolled;

        private System.Drawing.Point _lastHoverPos;
        public System.Drawing.Point? LastHoverPosition => _lastHoverPos != System.Drawing.Point.Empty ? _lastHoverPos : null;
        private DateTime _lastHoverTime = DateTime.MinValue;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    if (_isEnabled)
                    {
                        StartHookThread();
                    }
                    else
                    {
                        StopHookThread();
                    }
                }
            }
        }

        public TrayScrollHook(NotifyIcon notifyIcon, bool isEnabled = true)
        {
            _notifyIcon = notifyIcon;
            _isEnabled = isEnabled;

            _notifyIcon.MouseMove += (s, e) =>
            {
                _lastHoverPos = Cursor.Position;
                _lastHoverTime = DateTime.UtcNow;
            };

            _notifyIcon.MouseDown += (s, e) =>
            {
                _lastHoverPos = Cursor.Position;
                _lastHoverTime = DateTime.UtcNow;
            };

            ExtractNotifyIconIdentifiers();
            if (_isEnabled)
            {
                StartHookThread();
            }
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

        private void StartHookThread()
        {
            if (_hookThread != null && _hookThread.IsAlive) return;

            _startedEvent.Reset();
            _hookThread = new Thread(HookThreadProc)
            {
                IsBackground = true,
                Name = "LuminarkTrayScrollHookThread",
                Priority = ThreadPriority.Highest
            };
            _hookThread.SetApartmentState(ApartmentState.STA);
            _hookThread.Start();
            _startedEvent.Wait(2000);
        }

        private void StopHookThread()
        {
            if (_hookThreadId != 0)
            {
                NativeMethods.PostThreadMessage(_hookThreadId, NativeMethods.WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
            }
            if (_hookThread != null && _hookThread.IsAlive)
            {
                _hookThread.Join(1000);
                _hookThread = null;
            }
            _hookThreadId = 0;
            _hookId = IntPtr.Zero;
        }

        private void HookThreadProc()
        {
            try
            {
                _hookThreadId = NativeMethods.GetCurrentThreadId();
                _proc = HookCallback;
                using var curProcess = System.Diagnostics.Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);
                _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, moduleHandle, 0);
                App.Log($"[TrayScrollHook] Hook started on dedicated thread (tid={_hookThreadId}). HookId={_hookId}");
            }
            catch (Exception ex)
            {
                App.Log($"[TrayScrollHook] Failed to start hook on dedicated thread: {ex.Message}");
            }
            finally
            {
                _startedEvent.Set();
            }

            // Dedicated Win32 message pump with ThreadPriority.Highest and zero UI/GC work.
            // Guarantees that Windows OS hook queries return in microseconds.
            while (!_disposed && NativeMethods.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessage(ref msg);
            }

            if (_hookId != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.UnhookWindowsHookEx(_hookId);
                }
                catch { }
                _hookId = IntPtr.Zero;
                App.Log("[TrayScrollHook] Hook removed.");
            }
        }

        [ThreadStatic]
        private static StringBuilder? _classNameBuffer;

        private static string GetWindowClassName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return string.Empty;
            _classNameBuffer ??= new StringBuilder(256);
            _classNameBuffer.Clear();
            NativeMethods.GetClassName(hwnd, _classNameBuffer, _classNameBuffer.Capacity);
            return _classNameBuffer.ToString();
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
            if (nCode >= 0 && (int)wParam == NativeMethods.WM_MOUSEWHEEL && _isEnabled)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
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

                // 2. Hover proximity tracking: cursor hasn't moved far from where hover occurred
                if (!isOverTray && (DateTime.UtcNow - _lastHoverTime).TotalSeconds < 3.0)
                {
                    int dx = Math.Abs(hookStruct.pt.x - _lastHoverPos.X);
                    int dy = Math.Abs(hookStruct.pt.y - _lastHoverPos.Y);

                    if (dx <= 48 && dy <= 48 && IsTaskbarOrTrayWindow(hookStruct.pt))
                    {
                        isOverTray = true;
                    }
                }

                if (isOverTray)
                {
                    _lastHoverTime = DateTime.UtcNow;
                    short delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                    App.Log($"[TrayScrollHook] Scrolled over tray icon: delta={delta}");
                    
                    var action = Scrolled;
                    if (action != null)
                    {
                        ThreadPool.QueueUserWorkItem(_ => action(delta));
                    }
                    return (IntPtr)1; // Consume so taskbar doesn't scroll
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            _disposed = true;
            StopHookThread();
            _startedEvent.Dispose();
        }
    }
}
