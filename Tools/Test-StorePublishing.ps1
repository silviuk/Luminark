$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/StorePublishing.ps1"
function Must-Throw([scriptblock]$Action, [string]$Name) {
    $threw = $false
    try { & $Action } catch { $threw = $true }
    if (-not $threw) { throw "Expected rejection: $Name" }
    Write-Host "PASS: $Name"
}
function Submission([string]$Version = '1.1.19.0') {
    return [pscustomobject]@{
        id = 'test'; applicationCategory = 'UtilitiesAndTools'; targetPublishMode = 'Immediate'
        applicationPackages = @([pscustomobject]@{version=$Version; architecture='x64'; fileStatus='Uploaded'})
        packageDeliveryOptions = [pscustomobject]@{packageRollout=[pscustomobject]@{isPackageRollout=$false}}
    }
}
Assert-LuminarkSubmission -Submission (Submission) -Version '1.1.19'
Write-Host 'PASS: exact requested submission accepted'
Must-Throw { Assert-LuminarkSubmission -Submission (Submission '1.1.18.0') -Version '1.1.19' } 'old package cannot satisfy a newer release'
$sub = Submission
$sub.applicationCategory = 'Personalization_Themes'
Must-Throw { Assert-LuminarkSubmission -Submission $sub -Version '1.1.19' } 'theme category rejected'
$sub = Submission
$sub.targetPublishMode = 'Manual'
Must-Throw { Assert-LuminarkSubmission -Submission $sub -Version '1.1.19' } 'held publication rejected'
$sub = Submission
$sub.packageDeliveryOptions.packageRollout.isPackageRollout = $true
Must-Throw { Assert-LuminarkSubmission -Submission $sub -Version '1.1.19' } 'partial rollout rejected'
$sub = Submission
$sub.applicationPackages[0].fileStatus = 'PendingDelete'
Must-Throw { Assert-LuminarkSubmission -Submission $sub -Version '1.1.19' } 'deleted package rejected'
foreach ($status in 'CommitStarted','PreProcessing','Certification','Release','PendingPublication','Publishing','Published') {
    if ((Get-LuminarkSubmissionAction $status) -ne 'Verify') { throw "Unsafe action for $status" }
}
foreach ($status in 'None','PendingCommit','CommitFailed','PreProcessingFailed','CertificationFailed','ReleaseFailed','PublishFailed') {
    if ((Get-LuminarkSubmissionAction $status) -ne 'Edit') { throw "Wrong action for $status" }
}
Must-Throw { Get-LuminarkSubmissionAction 'UnexpectedStatus' } 'unknown submission status rejected'
Write-Host 'PASS: submission lifecycle cases'

# Validate the package contents, not its filename.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$temp = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.msix')
try {
    $zip = [IO.Compression.ZipFile]::Open($temp, [IO.Compression.ZipArchiveMode]::Create)
    $writer = [IO.StreamWriter]::new($zip.CreateEntry('AppxManifest.xml').Open())
    $writer.Write('<Package><Identity Name="SilviuVlasceanu.Luminark" Publisher="CN=FCAD1026-DA23-45D7-835A-3FF4ADD435D4" Version="1.1.18.0" ProcessorArchitecture="x64"/><Applications><Application Executable="Luminark.exe"/></Applications></Package>')
    $writer.Dispose(); $zip.Dispose()
    Must-Throw { Get-LuminarkPackageIdentity -Path $temp -Version '1.1.19' } 'wrong embedded MSIX version rejected'
    $null = Get-LuminarkPackageIdentity -Path $temp -Version '1.1.18'
    Write-Host 'PASS: matching embedded MSIX identity accepted'
} finally { Remove-Item -LiteralPath $temp -ErrorAction SilentlyContinue }
