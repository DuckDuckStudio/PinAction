[Setup]
AppName=PinAction
AppVersion=develop
DefaultDirName={pf}\DuckStudio\PinAction
VersionInfoCopyright=版权所有 (c) 2026 鸭鸭「カモ」
LicenseFile=LICENSE.txt
AllowNoIcons=yes
AppPublisher=鸭鸭「カモ」
AppPublisherURL=https://duckduckstudio.github.io/yazicbs.github.io/
OutputDir=Release
OutputBaseFilename=PinAction_Setup
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Files]
Source: "PinAction\bin\Release\net10.0\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "{sys}\cmd.exe"; Parameters: "/C setx PATH ""{app};%PATH%"" /M"; Flags: runhidden

[UninstallRun]
Filename: "{sys}\cmd.exe"; Parameters: "/C setx PATH ""%PATH:{app};=%"" /M"; Flags: runhidden; RunOnceId: UninstallSetPath
