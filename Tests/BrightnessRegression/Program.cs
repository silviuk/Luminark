using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Lumina.Models;
using Lumina.Services;
using Lumina.ViewModels;

internal static class Program
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static void Main(string[] args)
    {
        Lumina.App.SuppressLogging = true; // Fixtures must never write to the user's application log.
        try
        {
            if (args.Contains("--hardware-smoke"))
            {
                uint? restore = null;
                int option = Array.IndexOf(args, "--restore-brightness");
                if (option >= 0)
                {
                    if (option + 1 >= args.Length || !uint.TryParse(args[option + 1], out uint value) || value > 100)
                        throw new ArgumentException("Expected a restore brightness between 0 and 100.");
                    restore = value;
                }
                HardwareChecks.Run(restore);
            }
            else Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.ExitCode = 1;
        }
        finally { Application.Current?.Shutdown(); }
    }

    private static void Run()
    {
        _ = new Application();
        CheckHardwareBurst(linked: true);
        CheckHardwareBurst(linked: false);
        CheckHardwareBurst(linked: true, enabled: false);
        CheckRapidDebounce();
        ReliabilityChecks.Run();
        ThemeChecks.Run();
        Console.WriteLine("PASS: rapid hardware events, linked/independent/disabled sync, user edits, debounce cancellation");
    }

    private static void CheckHardwareBurst(bool linked, bool enabled = true)
    {
        using var service = new MonitorService();
        // Bypass startup (monitor discovery, power settings, registry and WMI).
        // Exercise the real property subscriptions and event handler on the WPF dispatcher.
        var vm = (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
        Set(vm, "_monitorService", service);
        Set(vm, "_settings", new AppSettings { IsMonitorsLinked = linked, SyncLaptopBrightnessKeys = enabled });
        var monitor = new MonitorInfo { Id = "test", Type = MonitorType.WmiInternal, CurrentBrightness = 10 };
        Set(vm, "<Monitors>k__BackingField", new ObservableCollection<MonitorInfo> { monitor });
        Call(vm, "SetupMonitorEventsAndSettings", monitor, 1);
        var observed = new List<uint>();
        vm.BrightnessStepped += observed.Add;

        foreach (uint brightness in new uint[] { 20, 30, 40, 50, 60 })
            Call(vm, "OnInternalBrightnessChanged", null, brightness);
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        // Inspect before the debounce delay expires; no hardware writes should exist.
        var writes = (IDictionary)typeof(MonitorService).GetField("_workers", Private)!.GetValue(service)!;
        Assert(writes.Count == 0, "A hardware observation scheduled a write back to the laptop");
        Assert(monitor.CurrentBrightness == (enabled ? 60u : 10u), "Final hardware value was lost");
        Assert(observed.Count == (enabled ? 5 : 0), "Rapid hardware events were suppressed");
        if (enabled && linked) Assert(vm.MasterBrightness == 60, "Linked master brightness did not reach final value");

        // A real user edit must still reach SetBrightness. Unknown avoids native hardware calls.
        monitor.Type = MonitorType.Unknown;
        monitor.CurrentBrightness = 75;
        Assert(writes.Contains(monitor.Id), "Individual user edits no longer schedule brightness writes");
    }

    private static void CheckRapidDebounce()
    {
        using var service = new MonitorService();
        var monitor = new MonitorInfo { Id = "debounce-test", Type = MonitorType.Unknown };
        for (int i = 0; i < 2000; i++) service.SetBrightness(monitor, (uint)(i % 101));
        Thread.Sleep(150);
        Assert(monitor.CurrentBrightness == 1999 % 101, "Rapid writes lost the latest target");
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private)!.SetValue(target, value);

    private static void Call(object target, string method, params object?[] args) =>
        target.GetType().GetMethod(method, Private)!.Invoke(target, args);

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
