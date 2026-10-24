using Lumina.Models;
using Lumina.Services;

internal static class HardwareChecks
{
    internal static void Run()
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
                for (int i = 0; i < 100; i++)
                {
                    if (!service.TryReacquirePhysicalHandle(monitor)) throw new InvalidOperationException("Physical monitor handle recovery failed.");
                    if (service.OwnedPhysicalHandleCount != expected) throw new InvalidOperationException("Native handle ownership grew during recovery.");
                    _ = ReadBrightness(monitor);
                }
                Console.WriteLine($"PASS: external display brightness burst/readback and 100 recovery cycles ({monitor.FriendlyName})");
            }
            finally
            {
                service.SetBrightness(monitor, original);
                service.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
                if (ReadBrightness(monitor) != original) throw new InvalidOperationException("Original display brightness could not be restored.");
            }
        }
    }

    private static uint ReadBrightness(MonitorInfo monitor)
    {
        if (NativeMethods.GetMonitorBrightness(monitor.PhysicalHandle, out _, out uint value, out _)) return value;
        if (NativeMethods.GetVCPFeatureAndVCPFeatureReply(monitor.PhysicalHandle, 0x10, out _, out value, out _)) return value;
        throw new InvalidOperationException("The actual display brightness could not be read.");
    }
}
