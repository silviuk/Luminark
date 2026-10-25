using System.Runtime.InteropServices;
using System.Text;

namespace Lumina.Services;

internal interface IThemeWindowMessenger
{
    IEnumerable<(IntPtr Handle, string ClassName)> GetWindows();
    void SettingChanged(IntPtr handle, uint timeout);
    void ThemeChanged(IntPtr handle, uint timeout);
}

internal sealed class WindowsThemeNotifier : IThemeWindowMessenger
{
    internal static void Refresh() => Refresh(new WindowsThemeNotifier());

    internal static void Refresh(IThemeWindowMessenger messenger)
    {
        // Invalidate separate shell theme resources on every display, including
        // all secondary taskbars. Do not rely on the generic settings broadcast.
        foreach (var window in messenger.GetWindows())
        {
            if (window.ClassName is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) continue;
            App.LogDebug($"[ThemeService] Refreshing taskbar: {window.ClassName} ({window.Handle})");
            messenger.SettingChanged(window.Handle, 2000);
            messenger.ThemeChanged(window.Handle, 2000);
        }
        messenger.SettingChanged((IntPtr)NativeMethods.HWND_BROADCAST, 500);
    }

    public IEnumerable<(IntPtr Handle, string ClassName)> GetWindows()
    {
        var windows = new List<(IntPtr, string)>();
        EnumWindows((handle, _) =>
        {
            var name = new StringBuilder(256);
            if (GetClassName(handle, name, name.Capacity) > 0) windows.Add((handle, name.ToString()));
            return true;
        }, IntPtr.Zero);
        return windows;
    }

    public void SettingChanged(IntPtr handle, uint timeout)
    {
        if (NativeMethods.SendMessageTimeout(handle, NativeMethods.WM_SETTINGCHANGE, UIntPtr.Zero,
            "ImmersiveColorSet", NativeMethods.SMTO_ABORTIFHUNG, timeout, out _) == IntPtr.Zero)
            App.LogDebug($"[ThemeService] Settings notification timed out or failed: {handle}");
    }

    public void ThemeChanged(IntPtr handle, uint timeout)
    {
        if (SendMessageTimeout(handle, 0x031A /* WM_THEMECHANGED */, UIntPtr.Zero, IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG, timeout, out _) == IntPtr.Zero)
            App.LogDebug($"[ThemeService] Taskbar theme invalidation timed out or failed: {handle}");
    }

    private delegate bool EnumWindowsCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr handle, StringBuilder name, int count);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr handle, uint message, UIntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out UIntPtr result);
}
