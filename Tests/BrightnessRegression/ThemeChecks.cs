using System.Collections.Concurrent;
using System.Windows.Threading;
using Lumina.Services;

internal static class ThemeChecks
{
    internal static void Run()
    {
        CheckRefreshQueue();
        CheckLatestUiState();
        CheckAllTaskbars();
        Console.WriteLine("PASS: serialized/coalesced theme refresh, latest UI theme, all taskbar notifications, disposal");
    }

    private static void CheckRefreshQueue()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int calls = 0, active = 0, maxActive = 0, desired = 0;
        var observations = new ConcurrentQueue<int>();
        using var queue = new ThemeRefreshQueue(() =>
        {
            maxActive = Math.Max(maxActive, Interlocked.Increment(ref active));
            if (Interlocked.Increment(ref calls) == 1)
            {
                entered.Set();
                if (!release.Wait(5000)) throw new TimeoutException();
            }
            observations.Enqueue(Volatile.Read(ref desired));
            Interlocked.Decrement(ref active);
        });
        queue.Request();
        try
        {
            Must(entered.Wait(3000), "Theme refresh did not start");
            for (int i = 0; i < 10000; i++) { Volatile.Write(ref desired, i % 2); queue.Request(); }
        }
        finally { release.Set(); }
        Must(queue.Completion.Wait(5000), "Theme refresh did not drain");
        Must(calls == 2 && maxActive == 1, "Theme refresh requests overlapped or accumulated");
        Must(observations.Last() == 1, "Latest requested theme was lost");
        queue.Dispose();
        queue.Request();
        Must(calls == 2, "Disposed theme queue accepted work");

        using var stoppingEntered = new ManualResetEventSlim();
        using var stoppingRelease = new ManualResetEventSlim();
        int stoppingCalls = 0;
        using var stoppingQueue = new ThemeRefreshQueue(() =>
        {
            Interlocked.Increment(ref stoppingCalls);
            stoppingEntered.Set();
            if (!stoppingRelease.Wait(5000)) throw new TimeoutException();
        });
        stoppingQueue.Request();
        try
        {
            Must(stoppingEntered.Wait(3000), "Shutdown fixture did not start");
            stoppingQueue.Request();
            stoppingQueue.Dispose();
        }
        finally { stoppingRelease.Set(); }
        Must(stoppingQueue.Completion.Wait(5000) && stoppingCalls == 1, "Shutdown retained a pending shell refresh");
    }

    private static void CheckLatestUiState()
    {
        bool apps = false, system = false;
        var applied = new List<bool>();
        var events = new List<bool>();
        using var service = new ThemeService(() => apps, () => system,
            light => { system = light; apps = light; }, () => { }, applied.Add);
        service.ThemeChanged += events.Add;
        // An older light notification waits in the UI queue while a newer dark
        // command completes. Dispatching it must not replay the old light value.
        apps = system = true;
        service.RefreshFromSystem();
        service.SetTheme(false);
        Pump();
        Must(applied.All(light => !light) && events.Count == 0, "Stale queued theme overrode the latest command");
        applied.Clear();
        apps = system = true;
        for (int i = 0; i < 1000; i++) service.RefreshFromSystem();
        Pump();
        Must(applied.SequenceEqual(new[] { true }) && events.SequenceEqual(new[] { true }),
            "Duplicate preference events were not coalesced");
        system = false;
        service.RefreshFromSystem();
        Pump();
        Must(events.Count == 2, "A system-only theme change failed to update taskbar/tray state");
        int before = applied.Count;
        service.RefreshFromSystem();
        service.Dispose();
        service.SetTheme(false);
        Pump();
        Must(applied.Count == before && apps, "Disposed theme service changed state or ran a queued callback");
    }

    private static void CheckAllTaskbars()
    {
        var messenger = new Messenger();
        WindowsThemeNotifier.Refresh(messenger);
        Must(messenger.Sent.SequenceEqual(new[] { "settings:1", "theme:1", "settings:2", "theme:2",
            "settings:3", "theme:3", "settings:65535" }),
            "Refresh omitted a taskbar, failed to invalidate theme resources, or notified an unrelated window directly");
    }

    private sealed class Messenger : IThemeWindowMessenger
    {
        internal readonly List<string> Sent = new();
        public IEnumerable<(IntPtr Handle, string ClassName)> GetWindows() => new[]
        {
            ((IntPtr)1, "Shell_TrayWnd"), ((IntPtr)2, "Shell_SecondaryTrayWnd"),
            ((IntPtr)3, "Shell_SecondaryTrayWnd"), ((IntPtr)4, "OtherWindow")
        };
        public void SettingChanged(IntPtr handle, uint timeout) => Sent.Add($"settings:{handle}");
        public void ThemeChanged(IntPtr handle, uint timeout) => Sent.Add($"theme:{handle}");
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Must(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
