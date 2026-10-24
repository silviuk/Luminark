param(
    [Parameter(Mandatory)][int]$ProcessId,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateRange(1, 86400)][int]$DurationSeconds = 600,
    [ValidateRange(100, 60000)][int]$IntervalMilliseconds = 1000
)
$ErrorActionPreference = 'Stop'
# Read-only sampling; this does not trim, collect, or change the target process.
if (-not ('LuminarkResourceSampler' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LuminarkResourceSampler {
    [StructLayout(LayoutKind.Sequential)] public struct Memory {
        public uint cb, PageFaultCount;
        public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage,
            QuotaPagedPoolUsage, QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage,
            PagefileUsage, PeakPagefileUsage, PrivateUsage;
    }
    [DllImport("psapi.dll", SetLastError=true)]
    public static extern bool GetProcessMemoryInfo(IntPtr process, ref Memory memory, uint size);
    [DllImport("user32.dll")] public static extern uint GetGuiResources(IntPtr process, uint flags);
}
'@
}
$process = Get-Process -Id $ProcessId
$lastCpu = $process.TotalProcessorTime.TotalMilliseconds
$elapsed = [Diagnostics.Stopwatch]::StartNew()
$lastTime = 0.0
$samples = [Collections.Generic.List[object]]::new()
try {
    while ($elapsed.Elapsed.TotalSeconds -lt $DurationSeconds) {
        Start-Sleep -Milliseconds $IntervalMilliseconds
        $process.Refresh()
        if ($process.HasExited) { break }
        $now = $elapsed.Elapsed.TotalMilliseconds
        $cpu = $process.TotalProcessorTime.TotalMilliseconds
        $memory = [LuminarkResourceSampler+Memory]::new()
        $memory.cb = [Runtime.InteropServices.Marshal]::SizeOf($memory)
        if (-not [LuminarkResourceSampler]::GetProcessMemoryInfo($process.Handle, [ref]$memory, $memory.cb)) {
            throw 'Cannot read target process memory counters.'
        }
        $samples.Add([pscustomobject]@{
            ElapsedSeconds = [Math]::Round($now / 1000, 3)
            CpuPercent = [Math]::Round(100 * ($cpu - $lastCpu) / ($now - $lastTime) / [Environment]::ProcessorCount, 3)
            PrivateBytes = $process.PrivateMemorySize64
            WorkingSetBytes = $process.WorkingSet64
            PageFaults = $memory.PageFaultCount
            Handles = $process.HandleCount
            Threads = $process.Threads.Count
            GdiObjects = [LuminarkResourceSampler]::GetGuiResources($process.Handle, 0)
            UserObjects = [LuminarkResourceSampler]::GetGuiResources($process.Handle, 1)
        })
        $lastTime = $now
        $lastCpu = $cpu
    }
} finally {
    $samples | Export-Csv -LiteralPath $OutputPath -NoTypeInformation
    $process.Dispose()
}
