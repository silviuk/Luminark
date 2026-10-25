param(
    [Parameter(Mandatory)][string]$Exe,
    [Parameter(Mandatory)][string]$WorkDirectory
)
$ErrorActionPreference = 'Stop'
$exePath = (Resolve-Path -LiteralPath $Exe).Path
if (Get-Process Luminark -ErrorAction SilentlyContinue) {
    throw 'Close Luminark before testing apphost startup to avoid single-instance forwarding.'
}
$fixture = Join-Path $WorkDirectory ('runtime-search-' + [guid]::NewGuid().ToString('N'))
$privateRoot = Join-Path $fixture 'incomplete-private-dotnet'
New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
# A real hostfxr with no shared frameworks reproduces a private .NET installation
# that exists but cannot satisfy the application. Do not alter installed runtimes.
Copy-Item -LiteralPath (Join-Path $env:ProgramFiles 'dotnet/host') -Destination $privateRoot -Recurse
$logPath = Join-Path $env:LOCALAPPDATA 'Luminark/luminark.log'
foreach ($override in 'None', 'DOTNET_ROOT', 'DOTNET_ROOT_X64') {
    $tracePath = Join-Path $fixture "$override.trace.log"
    $oldLog = if (Test-Path -LiteralPath $logPath) { [IO.File]::ReadAllText($logPath) } else { '' }
    $start = [Diagnostics.ProcessStartInfo]::new($exePath, '--minimized')
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardError = $true
    foreach ($name in 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT_X86', 'DOTNET_ROOT(x86)') {
        $start.Environment.Remove($name) | Out-Null
    }
    if ($override -ne 'None') { $start.Environment[$override] = $privateRoot }
    $start.Environment['DOTNET_DISABLE_GUI_ERRORS'] = '1'
    $start.Environment['DOTNET_HOST_TRACE'] = '1'
    $start.Environment['DOTNET_HOST_TRACEFILE'] = $tracePath
    $app = $null
    try {
        $app = [Diagnostics.Process]::Start($start)
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $started = $false
        while ($timer.Elapsed.TotalSeconds -lt 30) {
            if ($app.WaitForExit(250)) {
                throw "Apphost failed with $override (exit $($app.ExitCode)): $($app.StandardError.ReadToEnd())"
            }
            if (-not (Test-Path -LiteralPath $logPath)) { continue }
            $newLog = [IO.File]::ReadAllText($logPath)
            $addedLog = if ($newLog.StartsWith($oldLog, [StringComparison]::Ordinal)) {
                $newLog.Substring($oldLog.Length)
            } else { $newLog }
            if ($addedLog -match 'Application_Startup completed successfully') { $started = $true; break }
        }
        if (-not $started) {
            throw "App did not confirm fresh successful startup with $override."
        }
        # The native host keeps its trace open exclusively until exit.
        $app.Kill()
        $app.WaitForExit()
        $trace = [IO.File]::ReadAllText($tracePath)
        if ($trace -notmatch '\.NET root search location options: 8' -or
            $trace.Contains("Using environment variable $override=")) {
            throw "Apphost did not enforce global runtime discovery with $override."
        }
        Write-Host "PASS: installed app starts using global .NET with $override"
    } finally {
        if ($app) {
            if (-not $app.HasExited) { $app.Kill(); $app.WaitForExit() }
            $app.Dispose()
        }
    }
}
