; Inštalátor programu SkVP (Inno Setup 6)
; Zostavuje sa automaticky cez GitHub Actions, verziu a priečinok s programom dodá zostavenie.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

#define AppName "SkVP"
#define AppExe "SkVP.exe"

[Setup]
AppId={{5B1E7C3A-9D2F-4A6B-8C1D-3E4F5A6B7C8D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=tarzanislav
AppComments=Evidencia, spracovanie a archivácia školského vzdelávacieho programu
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\SkVP
DefaultGroupName=SkVP
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=SkVP_Setup_{#AppVersion}
SetupIconFile=..\src\SkVP.App\Assets\skvp.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern
MinVersion=10.0.17763
CloseApplications=yes

[Languages]
#if FileExists(CompilerPath + "Languages\Slovak.isl")
Name: "sk"; MessagesFile: "compiler:Languages\Slovak.isl"
#else
Name: "en"; MessagesFile: "compiler:Default.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
; ukážkové dáta – program ich pri prvom spustení nahrá (dajú sa neskôr vymazať v programe)
Name: "testovaciasada"; Description: "Nainštalovať testovaciu sadu učebných osnov (ukážkové osnovy všetkých predmetov na vyskúšanie programu)"; GroupDescription: "Ukážkové dáta:"

[Registry]
Root: HKLM; Subkey: "Software\tarzanislav\SkVP"; ValueType: dword; ValueName: "TestovaciaSada"; ValueData: 1; Tasks: testovaciasada; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\tarzanislav\SkVP"; ValueType: dword; ValueName: "TestovaciaSada"; ValueData: 0; Tasks: not testovaciasada; Flags: uninsdeletekey

[Dirs]
; spoločný priečinok s databázou – zapisovať doň môžu všetci používatelia počítača
Name: "{commonappdata}\SkVP"; Permissions: users-modify
Name: "{commonappdata}\SkVP\Data"; Permissions: users-modify

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\SkVP"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\SkVP"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,SkVP}"; Flags: nowait postinstall skipifsilent
; po automatickej aktualizácii z programu (tichá inštalácia s parametrom /SPUSTIT=1) sa SkVP znova spustí
Filename: "{app}\{#AppExe}"; Flags: nowait runasoriginaluser; Check: SpustitPoAktualizacii

[Code]
function SpustitPoAktualizacii: Boolean;
begin
  Result := ExpandConstant('{param:SPUSTIT|0}') = '1';
end;

// Databáza (C:\ProgramData\SkVP) a zálohy (Dokumenty\SkVP\Zálohy) sa pri odinštalovaní nemažú.
