param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$tool = (Get-Content "$PSScriptRoot/ReleaseTools.json" -Raw | ConvertFrom-Json).innosetup
$setup = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.exe')
try {
    Invoke-WebRequest $tool.url -OutFile $setup
    if ((Get-FileHash $setup -Algorithm SHA256).Hash -ine $tool.sha256) { throw 'Inno Setup SHA256 mismatch.' }
    $install = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES',
        '/NORESTART', '/CURRENTUSER', "/DIR=`"$Destination`"") -WindowStyle Hidden -Wait -PassThru
    $compiler = Join-Path $Destination 'ISCC.exe'
    if ($install.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup installation failed.' }
    if ($env:GITHUB_ENV) { "INNO_COMPILER=$compiler" >> $env:GITHUB_ENV }
    Write-Output $compiler
} finally { Remove-Item -LiteralPath $setup -Force -ErrorAction SilentlyContinue }
