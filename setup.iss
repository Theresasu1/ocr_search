; Inno Setup Script for OCR File Search Tool
#define MyAppName "OCR文件搜索工具"
#define MyAppVersion "5.0.0"
#define MyAppPublisher "FileSearchTool Dev Team"
#define MyAppURL "https://github.com/"
#define MyAppExeName "FileSearchTool.exe"
#define MyAppIcon "icon\\screen2.ico"
#define MyAppCNName "OCR文件搜索工具"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{8D7A82A7-4F7B-4D8A-9F8B-8C8F8D8E8F9A}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=
PrivilegesRequired=admin
OutputDir=.
OutputBaseFilename=FileSearchTool_Setup_v{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"
Name: "quicklaunchicon"; Description: "创建快速启动栏快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked; OnlyBelowVersion: 6.1
Name: "startupicon"; Description: "开机自动启动"; GroupDescription: "其他选项:"; Flags: unchecked

[Files]
Source: "FileSearchTool\bin\Release\net6.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 确保图标文件被复制
Source: "icon\serach.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "icon\screen2.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\\{#MyAppCNName}"; Filename: "{app}\\{#MyAppExeName}"; IconFilename: "{app}\\screen2.ico"; Comment: "启动 {#MyAppCNName}"
Name: "{autodesktop}\\{#MyAppCNName}"; Filename: "{app}\\{#MyAppExeName}"; Tasks: desktopicon; IconFilename: "{app}\\screen2.ico"; Comment: "启动 {#MyAppCNName}"
Name: "{userstartup}\\{#MyAppCNName}"; Filename: "{app}\\{#MyAppExeName}"; Tasks: startupicon; IconFilename: "{app}\\screen2.ico"

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "立即运行 {#MyAppCNName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: files; Name: "{app}\\*.config"
Type: files; Name: "{app}\\*.db"