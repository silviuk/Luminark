$ErrorActionPreference = 'Stop'
$raw = dotnet list "$PSScriptRoot/../Luminark.csproj" package --vulnerable --include-transitive --format json
if ($LASTEXITCODE -ne 0) { throw 'Dependency advisory query failed.' }
$result = ($raw -join "`n") | ConvertFrom-Json
if ($result.problems) { throw 'Dependency audit returned problems; inspection is required.' }
foreach ($project in $result.projects) {
    foreach ($framework in $project.frameworks) {
        foreach ($package in @($framework.topLevelPackages) + @($framework.transitivePackages)) {
            if ($package.vulnerabilities) { throw "Vulnerable dependency: $($package.id) $($package.resolvedVersion)" }
        }
    }
}
Write-Host 'PASS: no vulnerable dependencies reported by the NuGet advisory query'
