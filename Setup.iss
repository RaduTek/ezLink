; ezLink installer (Inno Setup 5, Unicode edition 5.5+)
; Build: "C:\Program Files (x86)\Inno Setup 5\ISCC.exe" Setup.iss   (after build.cmd Release)
; Packs everything in bin\Release except *.pdb into bin\Setup.

#define AppName  "Remotek ezLink Software"
#define AppVersion   "1.0.0"
#define Publisher    "Remotek Corp."

[Setup]
AppId=ezLink
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={pf}\Remotek\ezLink
DefaultGroupName=Remotek
DisableWelcomePage=no
DisableProgramGroupPage=yes
DisableReadyPage=yes
PrivilegesRequired=admin
OutputDir=bin\Setup
OutputBaseFilename=ezLink-Setup-{#AppVersion}
SetupIconFile=ezLink\Resources\ezLink.ico
UninstallDisplayIcon={app}\ezLink.exe
UninstallDisplayName={#AppName}
Compression=zip
SolidCompression=no
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#Publisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\Release\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: recursesubdirs createallsubdirs ignoreversion restartreplace

; Drivers are embedded in the installer; only the set matching this OS and bitness is copied (flat) to {app}\drivers
; and installed from there. The files stay for the uninstaller (DPInst /U needs the INF).
Source: "drivers\dpinstx64.exe"; DestDir: "{app}\drivers"; Check: DriverSetWanted and IsWin64; Flags: ignoreversion
Source: "drivers\dpinstx86.exe"; DestDir: "{app}\drivers"; Check: DriverSetWanted and not IsWin64; Flags: ignoreversion
; Windows XP: a single INF covers both architectures and DPInst's preinstall aborts if any file the INF
; lists is missing, so the whole XP folder is copied (x86 and x64 files)
Source: "drivers\XP\*"; DestDir: "{app}\drivers"; Check: IsXPDriverSet; Flags: ignoreversion
; Windows Vista - 8.1
Source: "drivers\Vista\rtux64lh.*"; DestDir: "{app}\drivers"; Check: IsVistaDriverSet and IsWin64; Flags: ignoreversion
Source: "drivers\Vista\RTNicProp64.dll"; DestDir: "{app}\drivers"; Check: IsVistaDriverSet and IsWin64; Flags: ignoreversion
Source: "drivers\Vista\rtux86lh.*"; DestDir: "{app}\drivers"; Check: IsVistaDriverSet and not IsWin64; Flags: ignoreversion
Source: "drivers\Vista\RTNicProp32.dll"; DestDir: "{app}\drivers"; Check: IsVistaDriverSet and not IsWin64; Flags: ignoreversion

[Icons]
Name: "{commonprograms}\Remotek\ezLink"; Filename: "{app}\ezLink.exe"

[Registry]
Root: HKLM; Subkey: "Software\ezLink"; ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"; Flags: uninsdeletevalue uninsdeletekeyifempty

[Run]
Filename: "{app}\ezLink.exe"; Description: "Run ezLink"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
var
  RestartNeeded: Boolean;

function IsXPDriverSet: Boolean;
begin
  Result := (GetWindowsVersion shr 24) < 6;
end;

{ Windows Vista through 8.1; Windows 10 and later need no driver package }
function IsVistaDriverSet: Boolean;
begin
  Result := ((GetWindowsVersion shr 24) >= 6) and ((GetWindowsVersion shr 24) < 10);
end;

function DriverSetWanted: Boolean;
begin
  Result := IsXPDriverSet or IsVistaDriverSet;
end;

function DpInstName: String;
begin
  if IsWin64 then Result := 'dpinstx64.exe' else Result := 'dpinstx86.exe';
end;

function NeedRestart: Boolean;
begin
  Result := RestartNeeded;
end;

function IsNetFx20Installed: Boolean;
var
  Value: Cardinal;
  Key: String;
begin
  Key := 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v2.0.50727';
  Result := (RegQueryDWordValue(HKLM32, Key, 'Install', Value) and (Value = 1)) or
            (IsWin64 and RegQueryDWordValue(HKLM64, Key, 'Install', Value) and (Value = 1));
end;

procedure InstallNetFx20;
var
  Installer: String;
  ResultCode: Integer;
begin
  if IsNetFx20Installed then Exit;
  if IsWin64 then Installer := 'NetFx20SP2_x64.exe' else Installer := 'NetFx20SP2_x86.exe';
  Installer := ExpandConstant('{src}\redist\') + Installer;
  if not FileExists(Installer) then
  begin
    Log('.NET Framework 2.0 missing and ' + Installer + ' not found; skipping');
    Exit;
  end;
  WizardForm.StatusLabel.Caption := 'Installing .NET Framework 2.0...';
  if not Exec(Installer, '/q /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log('Warning: could not run ' + Installer)
  else if ResultCode = 3010 then
    RestartNeeded := True
  else if ResultCode <> 0 then
    Log(Format('Warning: .NET Framework 2.0 installer returned %d', [ResultCode]));
end;

procedure InstallDrivers;
var
  Root, DpInst: String;
  ResultCode: Integer;
begin
  if not DriverSetWanted then Exit;
  Root := ExpandConstant('{app}\drivers');
  DpInst := Root + '\' + DpInstName;
  { DPInst's exit code is a status bit field (installed package count in the low byte), so only log it }
  if Exec(DpInst, '/Q /SE /SA /SW /PATH "' + Root + '"', Root, SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    Log(Format('DPInst exit code 0x%x', [ResultCode]))
  else
    Log('Warning: could not run ' + DpInst);
end;

procedure RemoveDrivers;
var
  Dir, Inf: String;
  ResultCode: Integer;
begin
  Dir := ExpandConstant('{app}\drivers\');
  if FileExists(Dir + 'rtuxp.INF') then Inf := Dir + 'rtuxp.INF'
  else if FileExists(Dir + 'rtux64lh.INF') then Inf := Dir + 'rtux64lh.INF'
  else if FileExists(Dir + 'rtux86lh.INF') then Inf := Dir + 'rtux86lh.INF'
  else Exit;
  if FileExists(Dir + DpInstName) then
    Exec(Dir + DpInstName, '/U "' + Inf + '" /D /Q', Dir, SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    InstallNetFx20  { setuputil needs the framework, so install it before the app files }
  else if CurStep = ssPostInstall then
  begin
    { Admin helper: machine UUID, firewall rule, data dir }
    if not Exec(ExpandConstant('{app}\setuputil.exe'), '/install', '', SW_HIDE,
                ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
      Log(Format('Warning: setup helper /install failed (%d), see %%ALLUSERSPROFILE%%\ezLink\setup.log', [ResultCode]));
    InstallDrivers;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveDrivers;
    { Removes the firewall rule; UUID and trust store are kept }
    Exec(ExpandConstant('{app}\setuputil.exe'), '/uninstall', '', SW_HIDE,
         ewWaitUntilTerminated, ResultCode);
  end
  else if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ezLink');
end;
