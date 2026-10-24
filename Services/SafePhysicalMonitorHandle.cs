using Microsoft.Win32.SafeHandles;

namespace Lumina.Services;

internal sealed class SafePhysicalMonitorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafePhysicalMonitorHandle(IntPtr value) : base(ownsHandle: true) => SetHandle(value);
    protected override bool ReleaseHandle() => NativeMethods.DestroyPhysicalMonitor(handle);
}
