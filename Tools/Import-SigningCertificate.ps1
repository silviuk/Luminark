$ErrorActionPreference = 'Stop'
if (-not $env:CODESIGN_PFX_BASE64 -or -not $env:CODESIGN_PFX_PASSWORD) {
    throw 'Configure CODESIGN_PFX_BASE64 and CODESIGN_PFX_PASSWORD repository secrets before publishing signed releases.'
}
$path = Join-Path $env:RUNNER_TEMP ([guid]::NewGuid().ToString('N') + '.pfx')
try {
    [IO.File]::WriteAllBytes($path, [Convert]::FromBase64String($env:CODESIGN_PFX_BASE64))
    $password = ConvertTo-SecureString $env:CODESIGN_PFX_PASSWORD -AsPlainText -Force
    $cert = Import-PfxCertificate -FilePath $path -CertStoreLocation Cert:\CurrentUser\My -Password $password
    if (@($cert).Count -ne 1 -or -not $cert.HasPrivateKey) { throw 'Expected one code-signing certificate with a private key.' }
    "CODESIGN_THUMBPRINT=$($cert.Thumbprint)" >> $env:GITHUB_ENV
} finally {
    Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
}
