param(
    [Parameter(Mandatory)][string]$InstallerManifest,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version
)
$ErrorActionPreference = 'Stop'
[xml]$project = Get-Content "$PSScriptRoot/../Luminark.csproj" -Raw
if ($project.Project.PropertyGroup.TargetFramework -notmatch '^net(\d+)\.0-windows') {
    throw 'Unsupported runtime target.'
}
$dependency = 'Microsoft.DotNet.DesktopRuntime.' + $Matches[1]
$yaml = Get-Content -LiteralPath $InstallerManifest -Raw
if ($yaml -notmatch '(?m)^PackageIdentifier:\s*silviuk\.Luminark\s*$' -or
    $yaml -notmatch "(?m)^PackageVersion:\s*$([regex]::Escape($Version))\s*$") {
    throw 'Generated WinGet manifest does not match the requested Luminark release.'
}
$pattern = 'Microsoft\.DotNet\.DesktopRuntime\.\d+'
if ([regex]::Matches($yaml, $pattern).Count -ne 1) {
    throw 'Expected exactly one runtime dependency. Review the generated manifest before submission.'
}
$yaml = [regex]::Replace($yaml, $pattern, $dependency)
[IO.File]::WriteAllText((Resolve-Path -LiteralPath $InstallerManifest).Path, $yaml, [Text.UTF8Encoding]::new($false))
