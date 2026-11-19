#ifndef AppVersion
  #define AppVersion "0.2.20"
#endif
[Setup]
AppId={{A81436B5-2F6E-4354-8EE3-B38552B6A18C}
AppName=UNSC Relay
AppVersion={#AppVersion}
AppPublisher=Daniele / UNSC Relay
AppPublisherURL=https://github.com/sup3r93/unsc-relay-desktop
DefaultDirName={localappdata}\Programs\UNSC Relay
UsePreviousAppDir=yes
DisableDirPage=auto
DefaultGroupName=UNSC Relay
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=UNSC-Relay-Setup-{#AppVersion}
SetupIconFile=..\src\UnscRelay.App\Assets\Relay.ico
UninstallDisplayIcon={app}\UNSC Relay.exe
Compression=lzma2/fast
LZMANumBlockThreads=4
SolidCompression=yes
WizardStyle=modern dark polar includetitlebar
WizardSizePercent=140
WizardBackImageFile=installer-art\background.png
WizardBackColor=#091312
WizardImageFile=installer-art\wizard.png
WizardSmallImageFile=installer-art\header.png
WizardImageBackColor=$171409
WizardSmallImageBackColor=$1C190D
DisableWelcomePage=no
DisableFinishedPage=no
CloseApplications=yes
RestartApplications=no
SignTool=relay
SignedUninstaller=yes
DisableProgramGroupPage=yes
AllowCancelDuringInstall=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Messages]
italian.WelcomeLabel1=La tua console. Tutte le connessioni.
italian.WelcomeLabel2=Benvenuto in UNSC Relay {#AppVersion}.%n%nSSH, trasferimenti SFTP, editor remoto e desktop: un unico spazio di lavoro.%n%nScegli la cartella del programma. I dati personali restano separati dai file del programma.
italian.FinishedHeadingLabel=UNSC Relay è pronto
italian.FinishedLabel=UNSC Relay {#AppVersion} è stato preparato sul PC.%n%nPremi Fine per aprire la console.
italian.ReadyLabel1=Pronto a preparare la console
english.WelcomeLabel1=Your console. Every connection.
english.WelcomeLabel2=Welcome to UNSC Relay {#AppVersion}.%n%nSSH, SFTP transfers, remote editing and desktops in one workspace.%n%nChoose the application folder. Your personal data stays separate from application files.
english.FinishedHeadingLabel=UNSC Relay is ready
english.FinishedLabel=UNSC Relay {#AppVersion} has been installed.%n%nSelect Finish to open the console.
english.ReadyLabel1=Ready to set up your console
french.WelcomeLabel1=Votre console. Toutes vos connexions.
french.WelcomeLabel2=Bienvenue dans UNSC Relay {#AppVersion}.%n%nSSH, transferts SFTP, édition distante et bureaux réunis dans un espace de travail.%n%nChoisissez le dossier du programme. Vos données personnelles restent séparées des fichiers du programme.
french.FinishedHeadingLabel=UNSC Relay est prêt
french.FinishedLabel=UNSC Relay {#AppVersion} a été installé.%n%nCliquez sur Terminer pour ouvrir la console.
french.ReadyLabel1=Prêt à préparer votre console
german.WelcomeLabel1=Ihre Konsole. Alle Verbindungen.
german.WelcomeLabel2=Willkommen bei UNSC Relay {#AppVersion}.%n%nSSH, SFTP-Übertragungen, Remote-Bearbeitung und Desktops in einem Arbeitsbereich.%n%nWählen Sie den Programmordner. Ihre persönlichen Daten bleiben von den Programmdateien getrennt.
german.FinishedHeadingLabel=UNSC Relay ist bereit
german.FinishedLabel=UNSC Relay {#AppVersion} wurde installiert.%n%nWählen Sie Fertigstellen, um die Konsole zu öffnen.
german.ReadyLabel1=Bereit zur Einrichtung Ihrer Konsole
spanish.WelcomeLabel1=Tu consola. Todas tus conexiones.
spanish.WelcomeLabel2=Bienvenido a UNSC Relay {#AppVersion}.%n%nSSH, transferencias SFTP, edición remota y escritorios en un solo espacio de trabajo.%n%nElige la carpeta del programa. Tus datos personales permanecen separados de los archivos del programa.
spanish.FinishedHeadingLabel=UNSC Relay está listo
spanish.FinishedLabel=UNSC Relay {#AppVersion} se ha instalado.%n%nPulsa Finalizar para abrir la consola.
spanish.ReadyLabel1=Todo listo para preparar tu consola

[CustomMessages]
italian.DesktopShortcut=Crea un collegamento sul desktop
italian.OpenRelay=Avvia UNSC Relay
english.DesktopShortcut=Create a desktop shortcut
english.OpenRelay=Open UNSC Relay
french.DesktopShortcut=Créer un raccourci sur le bureau
french.OpenRelay=Ouvrir UNSC Relay
german.DesktopShortcut=Desktopverknüpfung erstellen
german.OpenRelay=UNSC Relay öffnen
spanish.DesktopShortcut=Crear un acceso directo en el escritorio
spanish.OpenRelay=Abrir UNSC Relay

[LangOptions]
DialogFontName=Segoe UI
DialogFontSize=10

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; Flags: unchecked

[Files]
Source: "..\src\UnscRelay.App\Assets\Relay.ico"; Flags: dontcopy
Source: "..\artifacts\distribution\*"; DestDir: "{app}"; Excludes: "\.smoke-data,\.smoke-data\*,\smoke-result.json,\Librerie\.smoke-data,\Librerie\.smoke-data\*,\Librerie\smoke-result.json,*smoke-result.json,*.pdb"; Flags: ignoreversion recursesubdirs

[InstallDelete]
#include "..\artifacts\native\legacy-files.iss"

[Icons]
Name: "{group}\UNSC Relay"; Filename: "{app}\UNSC Relay.exe"
Name: "{autodesktop}\UNSC Relay"; Filename: "{app}\UNSC Relay.exe"; Tasks: desktopicon; Check: DesktopShortcutNeedsCreation

[Run]
Filename: "{app}\UNSC Relay.exe"; Description: "{cm:OpenRelay}"; Flags: nowait runasoriginaluser

[Code]
var MissionFooter: TNewStaticText;
function LoadImage(Instance: THandle; Name: String; Kind, Width, Height, Flags: Integer): THandle;
  external 'LoadImageW@user32.dll stdcall';
function GetWindow(Handle: THandle; Command: Cardinal): THandle;
  external 'GetWindow@user32.dll stdcall';
procedure SetInstallerIcon(Handle: THandle; Icon: THandle; Kind: Integer);
begin
  if (Handle <> 0) and (Icon <> 0) then SendMessage(Handle, $0080, Kind, Icon);
end;


function IsRelayUpdate(): Boolean;
begin
  Result := (ExpandConstant('{param:RELAYUPDATE|0}') = '1') or
    (StrToIntDef(ExpandConstant('{param:WAITFORPID|0}'), 0) > 0);
end;

procedure InitializeWizard();
var LargeIcon, SmallIcon, OwnerHandle: THandle;
begin
  ExtractTemporaryFile('Relay.ico');
  LargeIcon := LoadImage(0, ExpandConstant('{tmp}\Relay.ico'), 1, ScaleX(32), ScaleY(32), $0010);
  SmallIcon := LoadImage(0, ExpandConstant('{tmp}\Relay.ico'), 1, ScaleX(16), ScaleY(16), $0010);
  SetInstallerIcon(WizardForm.Handle, LargeIcon, 1);
  SetInstallerIcon(WizardForm.Handle, SmallIcon, 0);
  OwnerHandle := GetWindow(WizardForm.Handle, 4);
  SetInstallerIcon(OwnerHandle, LargeIcon, 1);
  SetInstallerIcon(OwnerHandle, SmallIcon, 0);
  if IsRelayUpdate() then begin
    WizardForm.CancelButton.Visible := False;
    WizardForm.CancelButton.Enabled := False;
  end;
  WizardForm.WelcomeLabel1.AutoSize := False;
  WizardForm.WelcomeLabel1.Height := ScaleY(84);
  WizardForm.WelcomeLabel2.Top := WizardForm.WelcomeLabel1.Top + WizardForm.WelcomeLabel1.Height + ScaleY(10);
  WizardForm.WelcomeLabel2.Height := WizardForm.WelcomePage.ClientHeight - WizardForm.WelcomeLabel2.Top - ScaleY(16);
  WizardForm.WelcomeLabel1.Font.Size := 22;
  WizardForm.WelcomeLabel1.Font.Style := [fsBold];
  WizardForm.FinishedHeadingLabel.Font.Size := 22;
  WizardForm.FinishedHeadingLabel.Font.Style := [fsBold];
  WizardForm.PageNameLabel.Font.Size := 11;
  WizardForm.PageNameLabel.Height := ScaleY(24);
  WizardForm.PageDescriptionLabel.Top := WizardForm.PageNameLabel.Top + WizardForm.PageNameLabel.Height + ScaleY(4);
  WizardForm.ProgressGauge.Height := ScaleY(14);
  MissionFooter := TNewStaticText.Create(WizardForm);
  MissionFooter.Parent := WizardForm;
  MissionFooter.Left := ScaleX(14);
  MissionFooter.Top := WizardForm.CancelButton.Top + ScaleY(8);
  MissionFooter.Width := ScaleX(270);
  MissionFooter.Caption := 'UNSC RELAY / LOCAL WORKSTATION';
  MissionFooter.Font.Name := 'Consolas';
  MissionFooter.Font.Size := 8;
  MissionFooter.Font.Color := $0083CAB5;
  WizardForm.WelcomeLabel1.Font.Color := $0083CAB5;
  WizardForm.FinishedHeadingLabel.Font.Color := $0083CAB5;
  WizardForm.PageNameLabel.Font.Color := $0083CAB5;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if IsRelayUpdate() then begin
    WizardForm.CancelButton.Visible := False;
    WizardForm.CancelButton.Enabled := False;
  end;
end;

procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if IsRelayUpdate() then begin
    Cancel := False;
    Confirm := False;
  end;
end;

function DesktopShortcutNeedsCreation(): Boolean;
var ShortcutPath: String; Shell, Link: Variant;
begin
  ShortcutPath := ExpandConstant('{autodesktop}\UNSC Relay.lnk');
  Result := not FileExists(ShortcutPath);
  if not Result then begin
    try
      Shell := CreateOleObject('WScript.Shell'); Link := Shell.CreateShortcut(ShortcutPath);
      Result := CompareText(Link.TargetPath, ExpandConstant('{app}\UNSC Relay.exe')) <> 0;
    except
      Result := False; { Preserve an existing shortcut if inspection fails. }
    end;
  end;
end;

const SYNCHRONIZE = $00100000;
const WAIT_OBJECT_0 = 0;
const WAIT_TIMEOUT = 258;
function OpenProcess(Access: Cardinal; Inherit: Boolean; Pid: Cardinal): THandle;
  external 'OpenProcess@kernel32.dll stdcall';
function WaitForSingleObject(Handle: THandle; Milliseconds: Cardinal): Cardinal;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

function InitializeSetup(): Boolean;
var Pid: Integer; Handle: THandle; Status: Cardinal;
begin
  Result := True;
  Pid := StrToIntDef(ExpandConstant('{param:WAITFORPID|0}'), 0);
  if Pid <= 0 then Exit;
  Handle := OpenProcess(SYNCHRONIZE, False, Pid);
  if Handle = 0 then Exit;
  try
    Status := WaitForSingleObject(Handle, 60000);
    Result := Status = WAIT_OBJECT_0;
    if not Result then
      MsgBox('UNSC Relay non si è ancora chiuso. Riprova dopo aver chiuso il programma.', mbError, MB_OK);
  finally
    CloseHandle(Handle);
  end;
end;

{ Inno's temporary rename also needs room below the classic Windows path limit. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if Length(WizardDirValue) > 120 then
    Result := 'La cartella scelta è troppo lunga per i componenti inclusi. Scegli un percorso più breve (massimo 120 caratteri).';
end;
