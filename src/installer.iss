; Інсталятор AutoShorts (Inno Setup 6). Збирається в build.yml: ISCC /DAppVer=1.2.3 src\installer.iss
#ifndef AppVer
  #define AppVer "0.0.0"
#endif

[Setup]
AppId={{039EC930-A32B-4527-B3A5-55F93A91FB45}
AppName=AutoShorts
AppVersion={#AppVer}
AppVerName=AutoShorts {#AppVer}
AppPublisher=Dimonchikk
AppPublisherURL=https://dimonchikk.github.io/AutoShorts/
AppSupportURL=https://github.com/Dimonchikk/AutoShorts
AppUpdatesURL=https://github.com/Dimonchikk/AutoShorts/releases
VersionInfoVersion={#AppVer}
DefaultDirName={localappdata}\Programs\AutoShorts
DefaultGroupName=AutoShorts
DisableProgramGroupPage=yes
DisableDirPage=auto
DisableReadyPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=AutoShorts-Setup
SetupIconFile=app.ico
UninstallDisplayIcon={app}\AutoShorts.exe
UninstallDisplayName=AutoShorts
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
AppMutex=AutoShorts_SingleInstance
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "uk"; MessagesFile: "compiler:Languages\Ukrainian.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\dist\AutoShorts.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\AutoShorts"; Filename: "{app}\AutoShorts.exe"; Comment: "Нарізка відео у вертикальні шортси"
Name: "{autodesktop}\AutoShorts"; Filename: "{app}\AutoShorts.exe"; Tasks: desktopicon; Comment: "Нарізка відео у вертикальні шортси"

[Run]
Filename: "{app}\AutoShorts.exe"; Description: "{cm:LaunchProgram,AutoShorts}"; Flags: nowait postinstall skipifsilent
; після тихого оновлення з програми — запустити її знову
Filename: "{app}\AutoShorts.exe"; Flags: nowait; Check: WizardSilent
