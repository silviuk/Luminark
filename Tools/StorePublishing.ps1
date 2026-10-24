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
