$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$runtime = Get-Content "$root/installer/runtime.json" -Raw | ConvertFrom-Json
$include = Get-Content "$root/installer/Runtime.iss" -Raw
if ($runtime.version -notmatch '^10\.0\.[0-9]+$' -or
    $runtime.url -cne "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$($runtime.version)/windowsdesktop-runtime-$($runtime.version)-win-x64.exe" -or
    $runtime.sha256 -notmatch '^[a-fA-F0-9]{64}$' -or $runtime.sha512 -notmatch '^[a-fA-F0-9]{128}$' -or
    -not $include.Contains('#define DesktopRuntimeUrl "' + $runtime.url + '"') -or
    -not $include.Contains('#define DesktopRuntimeSha256 "' + $runtime.sha256 + '"') -or
    -not $include.Contains('#define DesktopRuntimeMinimumPatch ' + ([version]$runtime.version).Build)) {
    throw 'Pinned runtime metadata and installer constants do not agree.'
}
$installer = Get-Content "$root/installer/Luminark.iss" -Raw
if (-not $installer.Contains("'{#DesktopRuntimeSha256}', nil") -or
    -not $installer.Contains('Get-AuthenticodeSignature') -or $installer.Contains('Exec(''taskkill.exe''')) {
    throw 'Installer integrity or absolute executable path checks failed.'
}
$tools = Get-Content "$root/Tools/ReleaseTools.json" -Raw | ConvertFrom-Json
if ($tools.wingetcreate.url -cne "https://github.com/microsoft/winget-create/releases/download/v$($tools.wingetcreate.version)/wingetcreate.exe" -or
    $tools.wingetcreate.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid pinned WinGet tooling.' }
foreach ($file in Get-ChildItem "$root/.github/workflows/*.yml") {
    foreach ($line in Get-Content $file) {
        if ($line -match 'uses:\s+([^\s]+)' -and $Matches[1] -notmatch '^\./|^[^@]+@[a-f0-9]{40}$') {
            throw "Unpinned action in $($file.Name): $line"
        }
    }
}
Write-Host 'PASS: runtime checksum/publisher configuration and pinned release dependencies'
