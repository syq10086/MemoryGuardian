; Earth Guardian installer. SPDX-License-Identifier: GPL-3.0-only
#ifndef AppVersion
  #define AppVersion "0.6.1"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\earth-release-0.6.1"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{92B673D8-984D-49F8-AF90-31A304ED992A}
AppName=内存卫士
AppVersion={#AppVersion}
AppVerName=内存卫士 {#AppVersion}
AppPublisher=宋域强
AppPublisherURL=https://blog.csdn.net/syq10086?type=blog
AppSupportURL=https://blog.csdn.net/syq10086?type=blog
AppUpdatesURL=https://www.xiaopuwa.com/memory-guardian/
DefaultDirName={autopf}\EarthGuardian
DefaultGroupName=内存卫士
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=admin
; Allows isolated per-user install/uninstall smoke testing; normal launch is admin.
PrivilegesRequiredOverridesAllowed=commandline
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=MemoryGuardian-{#AppVersion}-Setup
SetupIconFile=..\orb\Assets\EarthGuardian.ico
UninstallDisplayIcon={app}\EarthGuardian.exe
LicenseFile=..\LICENSE
InfoBeforeFile=install-notes.txt
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
Uninstallable=yes
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName=内存卫士
VersionInfoDescription=内存卫士安装程序
VersionInfoCopyright=GPL v3; based on Windows Memory Cleaner by Igor Mundstein and contributors
SetupLogging=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Files]
Source: "{#SourceDir}\EarthGuardian.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\EarthGuardian.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\NOTICE.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\内存卫士"; Filename: "{app}\EarthGuardian.exe"; WorkingDir: "{app}"; IconFilename: "{app}\EarthGuardian.exe"; Tasks: desktopicon
Name: "{autoprograms}\内存卫士\内存卫士"; Filename: "{app}\EarthGuardian.exe"; WorkingDir: "{app}"
Name: "{autoprograms}\内存卫士\作者主页"; Filename: "https://blog.csdn.net/syq10086?type=blog"
Name: "{autoprograms}\内存卫士\更新与下载"; Filename: "https://www.xiaopuwa.com/memory-guardian/"
Name: "{autoprograms}\内存卫士\卸载内存卫士"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\EarthGuardian.exe"; Description: "运行内存卫士"; Flags: postinstall nowait skipifsilent unchecked

[Code]
// On upgrade, remove a legacy shortcut only if it points to this installation.
// Keep the AppId, executable name and settings path for upgrade compatibility.
procedure RemoveLegacyShortcut(Path, ExpectedTarget: String);
var
  Shell, Shortcut: Variant;
begin
  if not FileExists(Path) then exit;
  try
    Shell := CreateOleObject('WScript.Shell');
    Shortcut := Shell.CreateShortcut(Path);
    if CompareText(Shortcut.TargetPath, ExpectedTarget) = 0 then begin
      DeleteFile(Path);
      Log('Removed owned legacy shortcut: ' + Path);
    end;
  except
    Log('Could not inspect legacy shortcut: ' + Path);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    RemoveLegacyShortcut(ExpandConstant('{autodesktop}\地球卫士.lnk'), ExpandConstant('{app}\EarthGuardian.exe'));
    RemoveLegacyShortcut(ExpandConstant('{autoprograms}\地球卫士\地球卫士.lnk'), ExpandConstant('{app}\EarthGuardian.exe'));
    RemoveLegacyShortcut(ExpandConstant('{autoprograms}\地球卫士\卸载地球卫士.lnk'), ExpandConstant('{app}\unins000.exe'));
  end;
end;

function InitializeSetup(): Boolean;
var
  Release: Cardinal;
begin
  Result := (RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 533320));
  if not Result then
    MsgBox('此软件需要 Microsoft .NET Framework 4.8.1。请先从微软官网安装该组件，然后重新运行安装包。' + #13#10 + 'https://dotnet.microsoft.com/download/dotnet-framework/net481', mbError, MB_OK);
end;

procedure RemoveOwnedStartupTasks();
var
  Service, Folder, Tasks, Task, Actions, Action: Variant;
  I: Integer;
  Name, Command, AppExe: String;
begin
  AppExe := ExpandFileName(ExpandConstant('{app}\EarthGuardian.exe'));
  try
    Service := CreateOleObject('Schedule.Service');
    Service.Connect();
    Folder := Service.GetFolder('\');
    Tasks := Folder.GetTasks(1);
    for I := Tasks.Count downto 1 do begin
      Task := Tasks.Item(I);
      Name := Task.Name;
      if Pos('MemoryOrb-', Name) = 1 then begin
        Actions := Task.Definition.Actions;
        // Only delete our task if it has exactly one executable action targeting
        // this installation. Never remove a portable copy's task or another app.
        if Actions.Count = 1 then begin
          Action := Actions.Item(1);
          if Action.Path <> '' then begin
            Command := Action.Path;
            if CompareText(ExpandFileName(RemoveQuotes(Command)), AppExe) = 0 then begin
              Folder.DeleteTask(Name, 0);
              Log('Removed startup task owned by this installation: ' + Name);
            end;
          end;
        end;
      end;
    end;
  except
    Log('Startup task cleanup could not finish: ' + GetExceptionMessage);
    if not UninstallSilent then
      MsgBox('未能清除登录启动任务。请在 Windows 任务计划程序中检查指向本安装目录的 MemoryOrb 任务。', mbInformation, MB_OK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then RemoveOwnedStartupTasks();
end;
