param(
    [Parameter(Mandatory)][string[]]$Path,
    [Parameter(Mandatory)][string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$certificate = Get-Item "Cert:\CurrentUser\My\$CertificateThumbprint"
if (-not $certificate.HasPrivateKey -or $certificate.NotAfter -le (Get-Date) -or
    -not ($certificate.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.3')) {
    throw 'A valid code-signing certificate with a private key is required.'
}
$tool = Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe' |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $tool) { throw 'Windows SDK signtool.exe was not found.' }
foreach ($file in $Path) {
    $resolved = (Resolve-Path -LiteralPath $file).Path
    & $tool.FullName sign /sha1 $CertificateThumbprint /s My /fd SHA256 /tr $TimestampUrl /td SHA256 $resolved
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $resolved" }
    $signature = Get-AuthenticodeSignature -LiteralPath $resolved
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Thumbprint -ne $CertificateThumbprint -or
        -not $signature.TimeStamperCertificate) {
        throw "Signature, signer or timestamp verification failed: $resolved"
    }
}
