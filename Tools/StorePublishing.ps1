# Shared validation for release assets and Partner Center submission reuse.
function Get-LuminarkPackageIdentity {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Version)
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $Path))
    try {
        $entry = $zip.GetEntry('AppxManifest.xml')
        if (-not $entry) { throw 'MSIX has no AppxManifest.xml.' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $identity = $manifest.Package.Identity
        if ($identity.Name -cne 'SilviuVlasceanu.Luminark' -or
            $identity.Publisher -cne 'CN=FCAD1026-DA23-45D7-835A-3FF4ADD435D4' -or
            $identity.Version -ne "$Version.0" -or $identity.ProcessorArchitecture -ne 'x64') {
            throw "MSIX identity does not match Luminark $Version.0 x64: $($identity.OuterXml)"
        }
        if ($manifest.Package.Applications.Application.Executable -ne 'Luminark.exe') {
            throw 'MSIX does not launch Luminark.exe.'
        }
        # XML elements are enumerable in PowerShell; return stable metadata rather
        # than allowing an empty Identity element to disappear from the pipeline.
        return [pscustomobject]@{
            Name = [string]$identity.Name
            Publisher = [string]$identity.Publisher
            Version = [string]$identity.Version
            ProcessorArchitecture = [string]$identity.ProcessorArchitecture
        }
    } finally { $zip.Dispose() }
}

function Assert-LuminarkSubmission {
    param([Parameter(Mandatory)]$Submission, [Parameter(Mandatory)][string]$Version, [switch]$AllowPendingUpload)
    $packages = @($Submission.applicationPackages | Where-Object { $_.fileStatus -ne 'PendingDelete' })
    $parsed = $packages.Count -eq 1 -and $packages[0].version -eq "$Version.0" -and $packages[0].architecture -eq 'x64'
    # Partner Center keeps PendingUpload with no parsed version during CommitStarted.
    # The uploaded bytes were already checked locally; require the exact asset name here.
    $pending = $AllowPendingUpload -and $packages.Count -eq 1 -and
        $packages[0].fileStatus -eq 'PendingUpload' -and -not $packages[0].version -and
        $packages[0].fileName -ceq "Luminark-$Version.0-x64.msix"
    if (-not ($parsed -or $pending)) {
        $details = $packages | Select-Object fileName, version, architecture, fileStatus | ConvertTo-Json -Compress
        throw "Submission $($Submission.id) does not contain exactly Luminark $Version.0 x64. Packages: $details"
    }
    if ($pending) {
        Write-Host 'Upload accepted; Partner Center has not parsed the package version yet. This is not a published release.'
    }
    if ($Submission.applicationCategory -ne 'UtilitiesAndTools') {
        throw "Submission category is '$($Submission.applicationCategory)', expected UtilitiesAndTools."
    }
    if ($Submission.targetPublishMode -ne 'Immediate') {
        throw 'Submission is not configured for immediate publication after certification.'
    }
    if ($Submission.packageDeliveryOptions.packageRollout.isPackageRollout) {
        throw 'Gradual package rollout is enabled; the release must be available to all eligible users.'
    }
}

function Get-LuminarkSubmissionAction {
    param([Parameter(Mandatory)][string]$Status)
    switch ($Status) {
        { $_ -in 'None', 'PendingCommit', 'CommitFailed', 'PreProcessingFailed', 'CertificationFailed', 'ReleaseFailed', 'PublishFailed' } { return 'Edit' }
        { $_ -in 'CommitStarted', 'PreProcessing', 'Certification', 'Release', 'PendingPublication', 'Publishing', 'Published' } { return 'Verify' }
        'Canceled' { return 'Create' }
        default { throw "Unsupported Partner Center submission status: $Status" }
    }
}

function Get-LuminarkReleaseNotes {
    param([Parameter(Mandatory)][string]$Changelog, [Parameter(Mandatory)][string]$Version)
    $pattern = '(?ms)^##\s+\[' + [regex]::Escape($Version) + '\][^\r\n]*\r?\n(.*?)(?=^##\s|\z)'
    $match = [regex]::Match($Changelog, $pattern)
    if (-not $match.Success -or -not $match.Groups[1].Value.Trim()) { throw "No changelog entry for $Version." }
    $notes = "What's New in Luminark v${Version}:`n`n" + $match.Groups[1].Value.Trim()
    if ($notes.Length -gt 1500) { $notes = $notes.Substring(0, 1497) + '...' }
    return $notes
}

function Get-LuminarkStoreScreenshots {
    param([Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)][string]$Version)
    $manifest = Get-Content (Join-Path $Directory 'screenshots.json') -Raw | ConvertFrom-Json
    if ($manifest.version -cne $Version -or $manifest.language -ine 'en-us' -or
        @($manifest.screenshots).Count -lt 1 -or @($manifest.screenshots).Count -gt 10) {
        throw 'Screenshot manifest must identify the exact version, en-us listing, and 1-10 screenshots.'
    }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($shot in $manifest.screenshots) {
        if ($shot.fileName -notmatch '^[a-zA-Z0-9][a-zA-Z0-9.-]*\.png$' -or
            -not $names.Add($shot.fileName) -or $shot.description.Length -gt 200 -or
            [string]::IsNullOrWhiteSpace($shot.description)) { throw 'Invalid or duplicate screenshot metadata.' }
        $path = Join-Path $Directory $shot.fileName
        $bytes = [IO.File]::ReadAllBytes($path)
        if ($bytes.Length -lt 24 -or $bytes.Length -gt 50MB -or
            [Convert]::ToHexString($bytes[0..7]) -ne '89504E470D0A1A0A') { throw 'Invalid screenshot PNG.' }
        $width = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
        $height = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
        if ($width -lt 1366 -or $height -lt 768 -or $width -gt 3840 -or $height -gt 2160) {
            throw "Screenshot dimensions violate desktop Store limits: $width x $height."
        }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $shot.sha256) { throw 'Screenshot SHA256 mismatch.' }
        [pscustomobject]@{ fileName=$shot.fileName; description=$shot.description; path=$path }
    }
}

function Set-LuminarkListingScreenshots {
    param([Parameter(Mandatory)]$Submission, [Parameter(Mandatory)][array]$Screenshots)
    $listing = $Submission.listings.PSObject.Properties | Where-Object Name -ieq 'en-us'
    if (@($listing).Count -ne 1 -or -not $listing.Value.baseListing) { throw 'Expected one existing en-us base listing.' }
    $base = $listing.Value.baseListing
    $images = @()
    foreach ($image in @($base.images)) {
        if ($null -eq $image) { continue }
        if ($image.imageType -eq 'Screenshot') {
            # Existing uploaded assets must be explicitly retired; preserve logos
            # and other device-family assets. Pending files need not be retained.
            if ($image.fileStatus -eq 'PendingUpload') { continue }
            $image.fileStatus = 'PendingDelete'
        }
        $images += $image
    }
    foreach ($shot in $Screenshots) {
        $images += [pscustomobject]@{ fileName=$shot.fileName; fileStatus='PendingUpload';
            description=$shot.description; imageType='Screenshot' }
    }
    $base | Add-Member -NotePropertyName images -NotePropertyValue $images -Force
}

function Assert-LuminarkListingScreenshots {
    param([Parameter(Mandatory)]$Submission, [Parameter(Mandatory)][array]$Screenshots)
    $listing = $Submission.listings.PSObject.Properties | Where-Object Name -ieq 'en-us'
    $images = @($listing.Value.baseListing.images | Where-Object { $_.imageType -eq 'Screenshot' -and $_.fileStatus -ne 'PendingDelete' })
    if ($images.Count -ne $Screenshots.Count) { throw 'Submitted desktop screenshot count does not match.' }
    foreach ($shot in $Screenshots) {
        $found = @($images | Where-Object { $_.fileName -ceq $shot.fileName -and $_.description -ceq $shot.description })
        if ($found.Count -ne 1 -or $found[0].fileStatus -notin 'PendingUpload','Uploaded') {
            throw "Submitted screenshot is missing or mismatched: $($shot.fileName)"
        }
    }
}
