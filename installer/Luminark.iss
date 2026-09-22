; Luminark Inno Setup Installer Script
; Defines packaging parameters for Windows 10/11 x64

#define MyAppName "Luminark"
#define MyAppVersion "1.1.8"
#define MyAppPublisher "Silviu Vlasceanu"
#define MyAppURL "https://github.com/silviuk/Luminark"
#define MyAppExeName "Luminark.exe"

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
function IsDotNet8DesktopInstalled: Boolean;
var
  FindRec: TFindRec;
  SharedPath: String;
begin
  Result := False;
  // Check 64-bit Program Files
  SharedPath := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if DirExists(SharedPath) then
  begin
    if FindFirst(SharedPath + '\8.*', FindRec) then
    begin
      try
        repeat
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
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
    if FindFirst(SharedPath + '\8.*', FindRec) then
    begin
      try
        repeat
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
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
begin
  Result := '';
  Exec('taskkill.exe', '/F /IM Luminark.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(250);

  if not IsDotNet8DesktopInstalled then
  begin
    DownloadUrl := 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
    InstallerPath := ExpandConstant('{tmp}\dotnet-desktop-runtime-8.0-x64.exe');

    WizardForm.StatusLabel.Caption := 'Microsoft .NET 8 Desktop Runtime is required. Downloading official runtime...';
    try
      DownloadTemporaryFile(DownloadUrl, 'dotnet-desktop-runtime-8.0-x64.exe', 'Downloading Microsoft .NET 8 Desktop Runtime (x64)...', nil);
    except
      Result := 'Failed to download Microsoft .NET 8 Desktop Runtime. Please install it manually from https://dotnet.microsoft.com/download/dotnet/8.0 and restart setup.';
      Exit;
    end;

    WizardForm.StatusLabel.Caption := 'Installing Microsoft .NET 8 Desktop Runtime (x64)...';
    if not Exec(InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := 'Failed to launch Microsoft .NET 8 Desktop Runtime installer (code: ' + IntToStr(ResultCode) + ').';
      Exit;
    end;

    if (ResultCode <> 0) and (ResultCode <> 3010) and (not IsDotNet8DesktopInstalled) then
    begin
      Result := 'Microsoft .NET 8 Desktop Runtime installation did not complete successfully (code: ' + IntToStr(ResultCode) + ').';
      Exit;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  Exec('taskkill.exe', '/F /IM Luminark.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(250);
end;
