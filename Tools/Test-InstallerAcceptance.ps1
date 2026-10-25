param([Parameter(Mandatory)][string]$Installer)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or -not $env:RUNNER_TEMP) {
    throw 'This prerequisite installation test may run only on a disposable GitHub-hosted Windows runner.'
}
$root = Split-Path $PSScriptRoot
$runtime = Get-Content "$root/installer/runtime.json" -Raw | ConvertFrom-Json
$work = Join-Path $env:RUNNER_TEMP ('LuminarkAcceptance-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
$runtimeSetup = Join-Path $work 'desktop-runtime.exe'
$runtimeDirectory = Join-Path $env:ProgramFiles "dotnet/shared/Microsoft.WindowsDesktop.App/$($runtime.version)"
$appDirectory = Join-Path $work 'app'
$appProcess = $null
try {
    Invoke-WebRequest $runtime.url -OutFile $runtimeSetup
    if ((Get-FileHash $runtimeSetup -Algorithm SHA256).Hash -ine $runtime.sha256) { throw 'Runtime SHA256 mismatch.' }
    $signature = Get-AuthenticodeSignature $runtimeSetup
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'Runtime Microsoft publisher verification failed.'
    }
    dotnet build-server shutdown
    $uninstall = Start-Process $runtimeSetup -ArgumentList '/uninstall /quiet /norestart' -WindowStyle Hidden -Wait -PassThru
    if ($uninstall.ExitCode -notin 0, 1605, 3010) { throw "Prerequisite removal failed: $($uninstall.ExitCode)" }
    # Hosted SDK images can contain runtime files without a separately removable
    # bundle. Isolate only compatible desktop runtime directories on this disposable
    # runner so the bootstrapper must supply an actually missing framework.
    $runtimeRoot = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'dotnet/shared/Microsoft.WindowsDesktop.App'))
    foreach ($directory in @(Get-ChildItem -LiteralPath $runtimeRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^10\.0\.\d+$' -and [version]$_.Name -ge [version]$runtime.version })) {
        $source = [IO.Path]::GetFullPath($directory.FullName)
        $destination = [IO.Path]::GetFullPath((Join-Path $work ('original-runtime-' + $directory.Name)))
        if ([IO.Path]::GetDirectoryName($source) -cne $runtimeRoot -or
            -not $destination.StartsWith($work + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Runtime fixture path escaped the explicit test directories.'
        }
        Move-Item -LiteralPath $source -Destination $destination
    }
    if (Test-Path -LiteralPath $runtimeDirectory) { throw 'The missing runtime precondition was not established.' }
    $installLog = Join-Path $work 'install.log'
    $install = Start-Process (Resolve-Path -LiteralPath $Installer).Path -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS=',
        "/DIR=`"$appDirectory`"", "/LOG=`"$installLog`"") -WindowStyle Hidden -Wait -PassThru
    if ($install.ExitCode -notin 0, 3010) { throw "Application setup failed: $($install.ExitCode)" }
    if (-not (Test-Path -LiteralPath $runtimeDirectory)) { throw 'The bootstrapper did not restore the required desktop runtime.' }
    $exe = Join-Path $appDirectory 'Luminark.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw 'Application binary was not installed.' }
    & "$PSScriptRoot/Test-AppHostRuntime.ps1" -Exe $exe -WorkDirectory $work
    Write-Host 'PASS: missing desktop runtime files, verified bootstrapper install/repair, application install and startup'
} finally {
    if ($appProcess -and -not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force }
    if (Test-Path -LiteralPath $work) {
        Get-ChildItem -LiteralPath $work -Filter '*.log' | ForEach-Object {
            Write-Host "Acceptance log: $($_.Name)"
            Get-Content -LiteralPath $_.FullName -Tail 30
        }
    }
}
