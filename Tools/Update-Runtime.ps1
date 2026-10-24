param([Parameter(Mandatory)][ValidatePattern('^10\.0\.\d+$')][string]$Version)
$ErrorActionPreference = 'Stop'
$metadata = Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'
$release = $metadata.releases | Where-Object { $_.windowsdesktop.version -eq $Version } | Select-Object -First 1
$runtime = $release.windowsdesktop.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.exe' }
if (@($runtime).Count -ne 1 -or $runtime.url -notlike 'https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/*') {
    throw 'Expected one official Windows desktop x64 runtime.'
}
$temporary = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.exe')
try {
    Invoke-WebRequest $runtime.url -OutFile $temporary
    if ((Get-FileHash $temporary -Algorithm SHA512).Hash -ine $runtime.hash) { throw 'Official runtime SHA512 mismatch.' }
    $signature = Get-AuthenticodeSignature $temporary
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'The runtime does not have a valid Microsoft publisher signature.'
    }
    $sha256 = (Get-FileHash $temporary -Algorithm SHA256).Hash
    $root = Split-Path $PSScriptRoot
    @{ version=$Version; url=$runtime.url; sha256=$sha256; sha512=$runtime.hash } |
        ConvertTo-Json | Set-Content (Join-Path $root 'installer/runtime.json') -Encoding utf8
    @(
        '#define DesktopRuntimeUrl "' + $runtime.url + '"'
        '#define DesktopRuntimeSha256 "' + $sha256 + '"'
        '#define DesktopRuntimeMinimumPatch ' + ([version]$Version).Build
    ) | Set-Content (Join-Path $root 'installer/Runtime.iss') -Encoding utf8
} finally { Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue }
