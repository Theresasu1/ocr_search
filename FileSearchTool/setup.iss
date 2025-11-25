; Inno Setup 脚本 - v2.0
[Setup]
AppName=文件内容索引与搜索工具
AppVersion=2.0.0
AppPublisher=FileSearchTool
AppPublisherURL=https://github.com/
AppSupportURL=https://github.com/
AppUpdatesURL=https://github.com/
DefaultDirName={autopf}\FileSearchTool
DefaultGroupName=文件内容索引与搜索工具
AllowNoIcons=yes
LicenseFile=
OutputDir=.\installer
OutputBaseFilename=FileSearchTool_Setup_v2.0
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\Release\net6.0-windows\win-x64\publish\FileSearchTool.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "bin\Release\net6.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\文件内容索引与搜索工具"; Filename: "{app}\FileSearchTool.exe"
Name: "{autodesktop}\文件内容索引与搜索工具"; Filename: "{app}\FileSearchTool.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FileSearchTool.exe"; Description: "{cm:LaunchProgram,文件内容索引与搜索工具}"; Flags: nowait postinstall skipifsilent