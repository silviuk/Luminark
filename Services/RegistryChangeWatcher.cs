using System.ComponentModel;
using Microsoft.Win32;

namespace Lumina.Services;

// The registration, registry key, event, and cancellation handle share one lifetime.
internal sealed class RegistryChangeWatcher : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private int _disposed;

    internal RegistryChangeWatcher(string path, Action changed)
    {
        _thread = new Thread(() => Watch(path, changed))
        {
            IsBackground = true,
            Name = "LuminarkRegistryWatcher"
        };
        _thread.Start();
    }

    private void Watch(string path, Action changed)
    {
        using var notification = new AutoResetEvent(false);
        var token = _stop.Token;
        var waits = new WaitHandle[] { notification, token.WaitHandle };
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Closing the key signals notification. Keep it open until the wait ends.
                using var key = Registry.CurrentUser.OpenSubKey(path);
                if (key == null)
                {
                    if (token.WaitHandle.WaitOne(2000)) return;
                    continue;
                }
                while (!token.IsCancellationRequested)
                {
                    notification.Reset();
                    int error = NativeMethods.RegNotifyChangeKeyValue(key.Handle.DangerousGetHandle(),
                        true, NativeMethods.REG_NOTIFY_CHANGE_NAME | NativeMethods.REG_NOTIFY_CHANGE_LAST_SET,
                        notification.SafeWaitHandle.DangerousGetHandle(), true);
                    if (error != 0) throw new Win32Exception(error);
                    _ready.Set();
                    if (WaitHandle.WaitAny(waits) == 1 || token.WaitHandle.WaitOne(80)) return;
                    changed();
                }
            }
            catch (Exception ex)
            {
                App.Log($"[RegistryWatcher] {ex.Message}");
                if (token.WaitHandle.WaitOne(2000)) return;
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        _thread.Join();
        _ready.Dispose();
        _stop.Dispose();
    }

    internal bool WaitUntilReady(TimeSpan timeout) => _ready.Wait(timeout);
}
