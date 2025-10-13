#define AppName "Everywhere"
#define AppPublisher "Sylinko"
#define AppExeName "Everywhere.Windows.exe"
#define AppVersion GetEnv("VERSION")

[Setup]
; --- Basic Application Information ---
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}

; --- Installer Settings ---
DefaultDirName={localappdata}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..
OutputBaseFilename=Everywhere-Windows-x64-Setup-v{#AppVersion}
PrivilegesRequired=lowest
Compression=lzma2

; --- UI and Icons ---
WizardStyle=modern
SetupIconFile=..\img\Everywhere.ico
UninstallDisplayIcon={app}\{#AppExeName}

; --- Registry ---
UninstallDisplayName={#AppName}
AppId={{D66EA41B-8DEB-4E5A-9D32-AB4F8305F664}}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copy all files from the publish directory to the installation directory {app}
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; AfterInstall: AfterMyProgInstall()

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autoprograms}\{#AppName}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

; Delete old version registry entries if they exist
[Code]
procedure AfterMyProgInstall();
var
  Identifier: String;
begin
  if RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Everywhere', 'Identifier', Identifier) and (Identifier = 'D66EA41B-8DEB-4E5A-9D32-AB4F8305F664') then
  begin
    RegDeleteKeyIncludingSubkeys(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\Everywhere');
  end;
end;

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent