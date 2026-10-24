using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Lumina.Models;
using Lumina.Services;
using Microsoft.Win32;

internal static class ReliabilityChecks
{
    internal static void Run()
    {
        CheckLatestWorker();
        CheckMonitorShutdown();
        CheckStaleDiscovery();
        CheckSettings();
        CheckRegistryWatcher();
        CheckScrollDispatcher();
        CheckLogger();
        Console.WriteLine("PASS: bounded/fair monitor queue, ordered writes, shutdown, settings backup/normalization, registry idle/change/shutdown, async logging");
    }

    private static void CheckLatestWorker()
    {
        var worker = new LatestMonitorWorker();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var final = new ManualResetEventSlim();
        var applied = new ConcurrentQueue<int>();
        int inFlight = 0, maxInFlight = 0;
        worker.Schedule(MonitorCommandKind.Brightness, () =>
        {
            maxInFlight = Math.Max(maxInFlight, Interlocked.Increment(ref inFlight));
            started.Set();
            if (!release.Wait(3000)) throw new TimeoutException();
            applied.Enqueue(0);
            Interlocked.Decrement(ref inFlight);
        });
        try
        {
            Must(started.Wait(3000), "First monitor command did not start");
            for (int i = 1; i <= 10000; i++)
            {
                int value = i;
                worker.Schedule(MonitorCommandKind.Brightness, () =>
                {
                    maxInFlight = Math.Max(maxInFlight, Interlocked.Increment(ref inFlight));
                    applied.Enqueue(value);
                    Interlocked.Decrement(ref inFlight);
                    final.Set();
                });
                worker.Schedule(MonitorCommandKind.Volume, () => applied.Enqueue(-1));
            }
            Must(worker.PendingCount == 2, "Queue grew or lost a command kind");
            release.Set();
            Must(final.Wait(3000), "Latest monitor target was not applied");
            worker.StopAsync().GetAwaiter().GetResult();
            Must(maxInFlight == 1, "Native commands overlapped");
            Must(applied.ToArray().SequenceEqual(new[] { 0, -1, 10000 }), "Commands were stale, reordered or volume was starved");
            worker.Schedule(MonitorCommandKind.Brightness, () => throw new Exception("Stopped worker accepted work"));
            Must(worker.PendingCount == 0, "Stopped worker retained work");
        }
        finally { release.Set(); worker.StopAsync().GetAwaiter().GetResult(); }
    }

    private static void CheckMonitorShutdown()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        int writes = 0;
        using var service = new MonitorService((_, _) =>
        {
            entered.Set();
            release.Wait(3000);
            Interlocked.Increment(ref writes);
        });
        var monitor = new MonitorInfo { Id = "fake", Type = MonitorType.Unknown };
        service.SetBrightness(monitor, 20);
        Must(entered.Wait(3000), "Fake native write did not start");
        service.SetBrightness(monitor, 80);
        var shutdown = Task.Run(service.Dispose);
        Must(!shutdown.Wait(100), "Shutdown did not drain the native write");
        release.Set();
        Must(shutdown.Wait(3000), "Shutdown did not finish after native I/O returned");
        service.SetBrightness(monitor, 50);
        Must(writes == 1, "Pending or post-shutdown writes reached hardware");
    }

    private static void CheckSettings()
    {
        string directory = Path.Combine(Environment.GetEnvironmentVariable("LUMINARK_TEST_WORK") ?? Path.GetTempPath(), "Luminark-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "settings.json");
            File.WriteAllText(path, "{\"DayBrightness\":999,\"BrightnessStep\":-7,\"CustomMonitorNames\":null,\"CustomInputNames\":{\"test\":null},\"VisibleInputCodes\":{\"test\":null},\"KnownExternalMonitorIds\":null}");
            var store = new SettingsService(path);
            var settings = store.Load();
            Must(ReferenceEquals(settings, store.Load()), "Consumers received different settings instances");
            Must(settings.DayBrightness == 100 && settings.BrightnessStep == 5, "Out-of-range values were not normalized");
            Must(settings.CustomMonitorNames != null && settings.CustomInputNames["test"] != null && settings.VisibleInputCodes["test"] != null, "Null collections were not normalized");
            settings.DayBrightness = 37;
            Must(store.Save(settings), "First settings save failed: " + store.LastSaveError);
            settings.DayBrightness = 62;
            Must(store.Save(settings), "Second settings save failed: " + store.LastSaveError);
            Must(JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("DayBrightness").GetUInt32() == 62, "Atomic save did not persist latest settings");
            File.WriteAllText(path, "{truncated");
            Must(new SettingsService(path).Load().DayBrightness == 37, "Corrupt primary did not recover the last valid backup");
            Must(!Directory.EnumerateFiles(directory, "*.tmp").Any(), "Save left temporary files behind");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void CheckStaleDiscovery()
    {
        using var applied = new ManualResetEventSlim();
        using var service = new MonitorService((_, _) => applied.Set());
        var monitor = new MonitorInfo { Id = "revision-test", Type = MonitorType.Unknown };
        var stale = new MonitorInfo { Id = monitor.Id, CurrentBrightness = 10 };
        service.SetBrightness(monitor, 80);
        Must(service.GetDetectedBrightness(stale) == 80, "Discovery overwrote a pending request");
        Must(applied.Wait(3000), "Latest request did not reach the fake hardware");
        Must(service.GetDetectedBrightness(stale) == 80, "Delayed discovery overwrote a completed newer request");
        service.ObserveBrightness(monitor, 45);
        Must(service.GetDetectedBrightness(stale) == 45, "Delayed discovery overwrote a hardware observation");
    }

    private static void CheckRegistryWatcher()
    {
        string path = @"Software\Luminark.Tests\" + Guid.NewGuid().ToString("N");
        using var key = Registry.CurrentUser.CreateSubKey(path);
        try
        {
            int changes = 0;
            using var changed = new ManualResetEventSlim();
            using (var watcher = new RegistryChangeWatcher(path, () =>
            {
                Interlocked.Increment(ref changes);
                changed.Set();
            }))
            {
                Must(watcher.WaitUntilReady(TimeSpan.FromSeconds(5)), "Registry notification did not register");
                Thread.Sleep(400);
                Must(changes == 0, "Idle registry watcher woke itself");
                key.SetValue("value", 1);
                Must(changed.Wait(3000), "Registry change was not delivered");
                Thread.Sleep(250);
                Must(changes == 1, "Watcher repeatedly woke without new changes");
            }
            key.SetValue("value", 2);
            Thread.Sleep(150);
            Must(changes == 1, "Disposed watcher delivered a callback");
            using var missing = new RegistryChangeWatcher(path + @"\missing", () => throw new Exception());
            var elapsed = Stopwatch.StartNew();
            missing.Dispose();
            Must(elapsed.Elapsed < TimeSpan.FromSeconds(1), "Missing-key retry did not cancel promptly");
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(path); }
    }

    private static void CheckLogger()
    {
        string directory = Path.Combine(Environment.GetEnvironmentVariable("LUMINARK_TEST_WORK") ?? Path.GetTempPath(), "Luminark-log-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "test.log");
        try
        {
            using (var log = new AsyncFileLogger(path, maxBytes: 1024))
                Parallel.For(0, 10000, i => log.Write(new string('x', 200) + i));
            Must(File.Exists(path), "Logger did not flush queued messages");
            Must(new FileInfo(path).Length < 40000, "Logger rotation did not bound file size by one batch");
            string old = path + ".old";
            if (File.Exists(old)) Must(new FileInfo(old).Length < 40000, "Rotated log was unbounded");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static void CheckScrollDispatcher()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var complete = new ManualResetEventSlim();
        int total = 0, calls = 0;
        using var dispatcher = new ScrollStepDispatcher(steps =>
        {
            int call = Interlocked.Increment(ref calls);
            if (call == 1) { entered.Set(); release.Wait(3000); }
            if (Interlocked.Add(ref total, steps) == 10001) complete.Set();
        });
        dispatcher.Add(1);
        Must(entered.Wait(3000), "Scroll dispatcher did not start");
        try
        {
            for (int i = 0; i < 10000; i++) dispatcher.Add(1);
            release.Set();
            Must(complete.Wait(3000), "Batched scroll lost steps");
            Must(calls == 2, "Scroll burst scheduled one callback per tick");
        }
        finally { release.Set(); }
    }

    private static void Must(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
