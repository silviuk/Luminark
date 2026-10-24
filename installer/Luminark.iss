; Luminark Inno Setup Installer Script
; Defines packaging parameters for Windows 10/11 x64

#define MyAppName "Luminark"
#define MyAppVersion "1.1.21"
#define MyAppPublisher "Silviu Vlasceanu"
#define MyAppURL "https://github.com/silviuk/Luminark"
#define MyAppExeName "Luminark.exe"
#include "Runtime.iss"

[Setup]
AppId={{D37E8420-56B0-4A9B-983C-C04C61F12345}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=Luminark-Setup-v{#MyAppVersion}
SetupIconFile=..\assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=force
CloseApplicationsFilter=*Luminark.exe*
RestartApplications=no
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Automatically start Luminark when Windows starts"; GroupDescription: "Automation:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startup

[InstallDelete]
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"
Type: files; Name: "{userstartup}\Lumina.lnk"

[UninstallDelete]
Type: files; Name: "{userstartup}\{#MyAppName}.lnk"
Type: files; Name: "{userstartup}\Lumina.lnk"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsDotNet10DesktopInstalled: Boolean;
var
  FindRec: TFindRec;
  SharedPath: String;
begin
  Result := False;
  // Check 64-bit Program Files
  SharedPath := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if DirExists(SharedPath) then
  begin
    if FindFirst(SharedPath + '\10.*', FindRec) then
    begin
      try
        repeat
          if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
            (Copy(FindRec.Name, 1, 5) = '10.0.') and
            (StrToIntDef(Copy(FindRec.Name, 6, 16), -1) >= {#DesktopRuntimeMinimumPatch}) then
          begin
            Result := True;
            Exit;
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;

  // Fallback: check standard Program Files
  SharedPath := ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if (not Result) and DirExists(SharedPath) then
  begin
    if FindFirst(SharedPath + '\10.*', FindRec) then
    begin
      try
        repeat
          if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
            (Copy(FindRec.Name, 1, 5) = '10.0.') and
            (StrToIntDef(Copy(FindRec.Name, 6, 16), -1) >= {#DesktopRuntimeMinimumPatch}) then
          begin
            Result := True;
            Exit;
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  InstallerPath: String;
  DownloadUrl: String;
  QuotedPath: String;
  VerifyCommand: String;
  SignatureLogPath: String;
  QuotedLogPath: String;
  SignatureDiagnostic: AnsiString;
begin
  Result := '';
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Luminark.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(250);

  if not IsDotNet10DesktopInstalled then
  begin
    DownloadUrl := '{#DesktopRuntimeUrl}';
    InstallerPath := ExpandConstant('{tmp}\dotnet-desktop-runtime-10.0-x64.exe');

    WizardForm.StatusLabel.Caption := 'Microsoft .NET 10 Desktop Runtime is required. Downloading official runtime...';
    try
      DownloadTemporaryFile(DownloadUrl, 'dotnet-desktop-runtime-10.0-x64.exe', '{#DesktopRuntimeSha256}', nil);
    except
      Result := 'Failed to verify or download Microsoft .NET 10 Desktop Runtime. Please install it from https://dotnet.microsoft.com/download/dotnet/10.0 and restart setup.';
      Exit;
    end;

    QuotedPath := InstallerPath;
    StringChangeEx(QuotedPath, #39, #39 + #39, True);
    SignatureLogPath := ExpandConstant('{tmp}\runtime-publisher.txt');
    QuotedLogPath := SignatureLogPath;
    StringChangeEx(QuotedLogPath, #39, #39 + #39, True);
    VerifyCommand := '$ErrorActionPreference=' + #39 + 'Stop' + #39 + '; try { ' +
      'Import-Module ($PSHOME + ' + #39 + '\Modules\Microsoft.PowerShell.Security\Microsoft.PowerShell.Security.psd1' + #39 + ') -Force; ' +
      '$s=Microsoft.PowerShell.Security\Get-AuthenticodeSignature -LiteralPath ' + #39 + QuotedPath + #39 +
      '; [IO.File]::WriteAllText(' + #39 + QuotedLogPath + #39 + ', [string]$s.Status + ' + #39 + ' ' + #39 + ' + $s.SignerCertificate.Subject)' +
      '; if ($s.Status -ne ' + #39 + 'Valid' + #39 + ' -or $s.SignerCertificate.Subject -notmatch ' + #39 +
      'O=Microsoft Corporation' + #39 + ') { exit 1 } } catch { [IO.File]::WriteAllText(' + #39 + QuotedLogPath + #39 + ', [string]$_); exit 1 }';
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -Command "' + VerifyCommand + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := 'The runtime publisher verification process could not be started.';
      Exit;
    end;
    if LoadStringFromFile(SignatureLogPath, SignatureDiagnostic) then Log('Runtime publisher: ' + String(SignatureDiagnostic));
    if ResultCode <> 0 then
    begin
      Result := 'The runtime publisher signature could not be verified. Installation was stopped.';
      Exit;
    end;
    WizardForm.StatusLabel.Caption := 'Installing Microsoft .NET 10 Desktop Runtime (x64)...';
    if not Exec(InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := 'Failed to launch Microsoft .NET 10 Desktop Runtime installer (code: ' + IntToStr(ResultCode) + ').';
      Exit;
    end;

    if (ResultCode <> 0) and (ResultCode <> 3010) then
    begin
      Result := 'Microsoft .NET 10 Desktop Runtime installation did not complete successfully (code: ' + IntToStr(ResultCode) + ').';
      Exit;
    end;
    if ResultCode = 3010 then NeedsRestart := True;
    // An SDK or damaged installation can retain MSI registration after its
    // runtime files disappear. Repair the same verified package in that case.
    if not IsDotNet10DesktopInstalled then
    begin
      if not Exec(InstallerPath, '/repair /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
      begin
        Result := 'Failed to repair the Microsoft .NET 10 Desktop Runtime.';
        Exit;
      end;
      if (ResultCode <> 0) and (ResultCode <> 3010) then
      begin
        Result := 'Microsoft .NET 10 Desktop Runtime repair failed (code: ' + IntToStr(ResultCode) + ').';
        Exit;
      end;
      if ResultCode = 3010 then NeedsRestart := True;
    end;
    if not IsDotNet10DesktopInstalled then
    begin
      Result := 'The required .NET 10 Desktop Runtime was not detected after installation.';
      Exit;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM Luminark.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(250);
end;
