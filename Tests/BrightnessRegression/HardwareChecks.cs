using Lumina.Models;
using Lumina.Services;

internal static class HardwareChecks
{
    internal static void Run(uint? restoreBrightness = null)
    {
        if (NativeMethods.IsWorkstationLocked())
            throw new InvalidOperationException("Unlock the workstation before hardware acceptance; sleeping displays will not be probed.");
        using var service = new MonitorService();
        var monitors = service.EnumerateMonitors().Where(m => m.IsActive && m.Type == MonitorType.DdcCi).ToArray();
        if (monitors.Length == 0) throw new InvalidOperationException("No active external DDC/CI display is available for hardware acceptance.");
        int expected = service.OwnedPhysicalHandleCount;
        foreach (var monitor in monitors)
        {
            uint original = ReadBrightness(monitor);
            uint restore = restoreBrightness ?? original;
            Console.WriteLine($"Hardware baseline: brightness={original}, handle={monitor.PhysicalHandle}");
            try
            {
                // A small reversible burst, followed by readback from the actual display.
                uint target = original;
                for (uint i = 1; i <= 5; i++)
                {
                    target = original + Math.Min(i, monitor.MaxBrightness - original);
                    service.SetBrightness(monitor, target);
                }
                service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
                if (ReadBrightness(monitor) != target) throw new InvalidOperationException("The display did not retain the final brightness target.");
                Console.WriteLine("PASS: actual brightness burst and readback");
                for (int i = 0; i < 100; i++)
                {
                    var previous = monitor.PhysicalHandle;
                    if (!service.TryReacquirePhysicalHandle(monitor)) throw new InvalidOperationException("Physical monitor handle recovery failed.");
                    if (i == 0) Console.WriteLine($"First recovery: {previous} -> {monitor.PhysicalHandle}");
                    if (service.OwnedPhysicalHandleCount != expected) throw new InvalidOperationException("Native handle ownership grew during recovery.");
                    _ = ReadBrightness(monitor);
                    Thread.Sleep(100);
                }
                Console.WriteLine($"PASS: external display brightness burst/readback and 100 recovery cycles ({monitor.FriendlyName})");
            }
            finally
            {
                service.SetBrightness(monitor, restore);
                service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
                if (ReadBrightness(monitor) != restore) throw new InvalidOperationException("Original display brightness could not be restored.");
                Console.WriteLine($"PASS: original display brightness restored ({restore})");
            }
        }
    }

    private static uint ReadBrightness(MonitorInfo monitor)
    {
        int error = 0;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (NativeMethods.GetMonitorBrightness(monitor.PhysicalHandle, out _, out uint value, out _)) return value;
            if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(monitor.PhysicalHandle, 0x10, out _, out value, out _)) return value;
            error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Thread.Sleep(100);
        }
        throw new InvalidOperationException($"The actual display brightness could not be read (handle={monitor.PhysicalHandle}, error=0x{error:X8}).");
    }
}
