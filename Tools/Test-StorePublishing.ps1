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
$sub = Submission
$sub.applicationPackages = @([pscustomobject]@{fileName='Luminark-1.1.19.0-x64.msix'; version=''; architecture=''; fileStatus='PendingUpload'})
Must-Throw { Assert-LuminarkSubmission -Submission $sub -Version '1.1.19' } 'unparsed package requires explicit pending mode'
Assert-LuminarkSubmission -Submission $sub -Version '1.1.19' -AllowPendingUpload
$sub.applicationPackages[0].fileName = 'Luminark-1.1.18.0-x64.msix'
Must-Throw { Assert-LuminarkSubmission -Submission $sub -Version '1.1.19' -AllowPendingUpload } 'pending older upload cannot satisfy newer release'

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

$notes = Get-LuminarkReleaseNotes -Changelog "## [1.1.23] - 2026-10-25`n### Fixed`n- Current fixes.`n## [1.1.22] - 2026-10-25`n- Older fixes." -Version '1.1.23'
if ($notes -notmatch 'Current fixes' -or $notes -match 'Older fixes') { throw 'Release notes crossed version boundaries.' }
Must-Throw { Get-LuminarkReleaseNotes -Changelog '## [1.1.22]' -Version '1.1.23' } 'missing requested changelog version rejected'
$shots = @([pscustomobject]@{fileName='current.png';description='Current dashboard'})
$sub = [pscustomobject]@{listings=[pscustomobject]@{'en-us'=[pscustomobject]@{baseListing=[pscustomobject]@{images=@(
    [pscustomobject]@{fileName='old.png';imageType='Screenshot';fileStatus='Uploaded';id='old-id'},
    [pscustomobject]@{fileName='logo.png';imageType='Icon';fileStatus='Uploaded';id='logo-id'},
    [pscustomobject]@{fileName='mobile.png';imageType='MobileScreenshot';fileStatus='Uploaded';id='mobile-id'}
)}}}}
Set-LuminarkListingScreenshots -Submission $sub -Screenshots $shots
Assert-LuminarkListingScreenshots -Submission $sub -Screenshots $shots
$images = $sub.listings.'en-us'.baseListing.images
if (($images | Where-Object id -eq 'old-id').fileStatus -ne 'PendingDelete' -or
    ($images | Where-Object id -eq 'logo-id').fileStatus -ne 'Uploaded' -or
    ($images | Where-Object id -eq 'mobile-id').fileStatus -ne 'Uploaded') { throw 'Screenshot replacement damaged existing listing assets.' }
$sub.listings.'en-us'.baseListing.images[-1].fileName = 'wrong.png'
Must-Throw { Assert-LuminarkListingScreenshots -Submission $sub -Screenshots $shots } 'wrong submitted screenshot rejected'
Write-Host 'PASS: version-specific notes, desktop screenshot replacement, logos and other device assets preserved'

$assetDirectory = Join-Path (Split-Path $PSScriptRoot) 'StoreAssets/1.1.23'
$validatedShots = @(Get-LuminarkStoreScreenshots -Directory $assetDirectory -Version '1.1.23')
if ($validatedShots.Count -ne 8) { throw 'Expected all three tabs and the tray window in both themes.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('luminark-screenshots-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
try {
    Copy-Item -LiteralPath (Join-Path $assetDirectory 'screenshots.json') -Destination (Join-Path $fixture 'screenshots.json')
    Must-Throw { Get-LuminarkStoreScreenshots -Directory $fixture -Version '1.1.22' } 'screenshot version mismatch rejected'
    $manifest = Get-Content (Join-Path $fixture 'screenshots.json') -Raw | ConvertFrom-Json
    $manifest.screenshots = @($manifest.screenshots[0])
    $manifest.screenshots[0].fileName = '../outside.png'
    $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $fixture 'screenshots.json')
    Must-Throw { Get-LuminarkStoreScreenshots -Directory $fixture -Version '1.1.23' } 'screenshot path traversal rejected'
    $manifest.screenshots[0].fileName = 'test.png'
    $manifest.screenshots[0].sha256 = '0' * 64
    Copy-Item -LiteralPath $validatedShots[0].path -Destination (Join-Path $fixture 'test.png')
    $manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $fixture 'screenshots.json')
    Must-Throw { Get-LuminarkStoreScreenshots -Directory $fixture -Version '1.1.23' } 'screenshot checksum mismatch rejected'
} finally {
    Remove-Item -LiteralPath (Join-Path $fixture 'screenshots.json'), (Join-Path $fixture 'test.png') -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $fixture -ErrorAction SilentlyContinue
}
Write-Host 'PASS: eight current screenshots, dimensions, checksums, version and safe paths'
