; Inno Setup script of Voicer. A page of the wizard, right after the greeting, asks whom to install for:
; - for the current user (the default, no administrator rights) into %LOCALAPPDATA%\Programs\Voicer. The voicer.portable
;   marker sits in its root, so the downloaded models, tools and settings stay in the Data folder inside the
;   installation, and an update never touches them;
; - for all users (Windows asks for administrator rights) into Program Files. A user cannot write there, so the marker
;   is left out: every user keeps the data in %LOCALAPPDATA%\Voicer\Data.
; Build it with Scripts/App/build-installer.ps1, which passes the version, the source folder, the pictures and the
; license pages.

#ifndef VoicerVersion
  #define VoicerVersion "0.1.0"
#endif
; The page of the project (PackageProjectUrl of Directory.Build.props): the link of the program in the list of
; installed apps.
#ifndef VoicerUrl
  #define VoicerUrl "https://github.com/DepthRel/Voicer"
#endif
#ifndef VoicerSource
  #define VoicerSource SourcePath + "..\..\artifacts\publish\win-x64"
#endif
#ifndef VoicerOutputDir
  #define VoicerOutputDir SourcePath + "..\..\artifacts\publish"
#endif
#ifndef VoicerIcon
  #define VoicerIcon SourcePath + "..\..\Sources\Apps\Voicer.UI\Assets\voicer.ico"
#endif
; The pictures of the wizard, drawn by Scripts/Assets/generate-installer-art.ps1 (build-installer.ps1 runs it).
#ifndef InstallerArt
  #define InstallerArt SourcePath + "..\..\artifacts\installer-art"
#endif
; The license page of every language: a short introduction and the English text of the GPL, written by
; Write-InstallerLicenses (Scripts/Shared/package-notes.ps1, run by the build script).
#ifndef InstallerLicense
  #define InstallerLicense SourcePath + "..\..\artifacts\installer-license"
#endif

[Setup]
AppId={{6E2B9C47-1F3A-4D58-A0C6-8B7E5D2F9A13}
AppName=Voicer
AppVersion={#VoicerVersion}
AppPublisher=Voicer
AppPublisherURL={#VoicerUrl}
AppSupportURL={#VoicerUrl}
AppUpdatesURL={#VoicerUrl}
AppCopyright=GPL-3.0-or-later
VersionInfoVersion={#VoicerVersion}
; {autopf} is %LOCALAPPDATA%\Programs for the current user and Program Files for all users.
DefaultDirName={autopf}\Voicer
DefaultGroupName=Voicer
DisableProgramGroupPage=yes
DisableDirPage=no
PrivilegesRequired=lowest
; The choice "only for me / for everyone" is a page of the wizard (see [Code]), not a dialog before it. Setup cannot
; change its rights half way, so choosing "for everyone" starts it again with /ALLUSERS, which this line permits. An
; update keeps the choice of the first installation.
PrivilegesRequiredOverridesAllowed=commandline
UsePreviousPrivileges=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 or newer.
MinVersion=10.0
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#VoicerIcon}
UninstallDisplayIcon={app}\Voicer.exe
; The logo instead of the stock box: a dark panel in the look of the application on the welcome page, the logo alone in
; the header of the others. Without the pictures (the script was not run) the stock ones are used.
#if FileExists(InstallerArt + "\voicer-wizard.bmp")
WizardImageFile={#InstallerArt}\voicer-wizard.bmp
WizardSmallImageFile={#InstallerArt}\voicer-wizard-small.bmp
#endif
OutputDir={#VoicerOutputDir}
OutputBaseFilename=Voicer-{#VoicerVersion}-setup
#ifdef VoicerSign
; The installer and the uninstaller are signed by the command build-installer.ps1 passes as /Svoicersign.
SignTool=voicersign
SignedUninstaller=yes
#endif
CloseApplications=yes
CloseApplicationsFilter=Voicer.exe,Voicer.UI.exe
; The wizard opens with a greeting, not with a question.
DisableWelcomePage=no
; The language is asked first, with the language of Windows preselected; a Windows in a language the wizard does not
; speak gets English, the first in [Languages].
ShowLanguageDialog=yes
LanguageDetectionMethod=uilanguage

[Languages]
; The first language is the one offered by default. A new language is one more line here and one more block in
; [Messages] and [CustomMessages].
Name: "english"; MessagesFile: "compiler:Default.isl"; LicenseFile: "{#InstallerLicense}\LICENSE.en.txt"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"; LicenseFile: "{#InstallerLicense}\LICENSE.de.txt"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"; LicenseFile: "{#InstallerLicense}\LICENSE.es.txt"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"; LicenseFile: "{#InstallerLicense}\LICENSE.fr.txt"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"; LicenseFile: "{#InstallerLicense}\LICENSE.pt.txt"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"; LicenseFile: "{#InstallerLicense}\LICENSE.ru.txt"

[Messages]
; The greeting of the first page; [name] and [name/ver] are filled in by Inno Setup.
english.WelcomeLabel1=Welcome to the [name] setup
english.WelcomeLabel2=Voicer turns the speech of audio and video files into text with the Whisper model, on the video card or the processor of this computer. Nothing leaves the computer.%n%nThis wizard installs [name/ver] on your computer.
german.WelcomeLabel1=Willkommen bei der Installation von [name]
german.WelcomeLabel2=Voicer verwandelt die Sprache in Audio- und Videodateien mit dem Whisper-Modell in Text, auf der Grafikkarte oder dem Prozessor dieses Computers. Nichts verlässt den Computer.%n%nDieser Assistent installiert [name/ver] auf Ihrem Computer.
spanish.WelcomeLabel1=Te damos la bienvenida a la instalación de [name]
spanish.WelcomeLabel2=Voicer convierte en texto el habla de archivos de audio y vídeo con el modelo Whisper, en la tarjeta gráfica o el procesador de este equipo. Nada sale del equipo.%n%nEste asistente instalará [name/ver] en tu equipo.
french.WelcomeLabel1=Bienvenue dans l'installation de [name]
french.WelcomeLabel2=Voicer transforme en texte la parole des fichiers audio et vidéo avec le modèle Whisper, sur la carte graphique ou le processeur de cet ordinateur. Rien ne quitte l'ordinateur.%n%nCet assistant va installer [name/ver] sur votre ordinateur.
portuguese.WelcomeLabel1=Bem-vindo à instalação do [name]
portuguese.WelcomeLabel2=O Voicer transforma em texto a fala de ficheiros de áudio e vídeo com o modelo Whisper, na placa gráfica ou no processador deste computador. Nada sai do computador.%n%nEste assistente vai instalar o [name/ver] no seu computador.
russian.WelcomeLabel1=Добро пожаловать в установку [name]
russian.WelcomeLabel2=Voicer превращает речь из аудио- и видеофайлов в текст с помощью модели Whisper — на видеокарте или процессоре этого компьютера. Ничего не уходит за его пределы.%n%nМастер установит [name/ver] на ваш компьютер.

[CustomMessages]
; Everything the wizard says beyond the standard messages of Inno Setup. %n is a line break, %1 a value.
english.DesktopIcon=Desktop shortcut
english.AdditionalTasks=Additional tasks:
english.LaunchVoicer=Start Voicer
english.ModeTitle=Who to install Voicer for
english.ModeQuestion=Who will use the program on this computer?
english.ModeHelp=“Only for me” installs the program into your profile and needs no administrator rights.%n“For everyone” installs it into Program Files: Windows asks for an administrator's permission once.
english.ModeOnlyMe=Only for me (%1)
english.ModeEveryone=For everyone
english.ModeRefused=Windows did not grant administrator rights (code %1), and Voicer cannot be installed for everyone without them.%n%nRun the setup as administrator to install for everyone: right-click the setup file and choose “Run as administrator”. Or choose “Only for me”.
english.DeleteDataPrompt=Also delete the data of Voicer (downloaded models, FFmpeg, settings) from the folder below?
english.DeleteDataHint=Choose "No" if you are going to install Voicer again: the models are several gigabytes.
german.DesktopIcon=Verknüpfung auf dem Desktop
german.AdditionalTasks=Zusätzlich:
german.LaunchVoicer=Voicer starten
german.ModeTitle=Für wen Voicer installiert wird
german.ModeQuestion=Wer wird das Programm auf diesem Computer verwenden?
german.ModeHelp=„Nur für mich“ installiert das Programm in Ihr Profil und benötigt keine Administratorrechte.%n„Für alle“ installiert es nach Program Files: Windows fragt einmal nach der Erlaubnis eines Administrators.
german.ModeOnlyMe=Nur für mich (%1)
german.ModeEveryone=Für alle
german.ModeRefused=Windows hat keine Administratorrechte erteilt (Code %1), und ohne sie kann Voicer nicht für alle installiert werden.%n%nStarten Sie die Installation als Administrator, um für alle zu installieren: Rechtsklick auf die Installationsdatei → „Als Administrator ausführen“. Oder wählen Sie „Nur für mich“.
german.DeleteDataPrompt=Auch die Daten von Voicer (heruntergeladene Modelle, FFmpeg, Einstellungen) aus dem Ordner unten löschen?
german.DeleteDataHint=Wählen Sie „Nein“, wenn Sie Voicer erneut installieren wollen: Die Modelle sind mehrere Gigabyte groß.
spanish.DesktopIcon=Acceso directo en el escritorio
spanish.AdditionalTasks=Adicional:
spanish.LaunchVoicer=Iniciar Voicer
spanish.ModeTitle=Para quién instalar Voicer
spanish.ModeQuestion=¿Quién usará el programa en este equipo?
spanish.ModeHelp=«Solo para mí» instala el programa en tu perfil y no necesita permisos de administrador.%n«Para todos» lo instala en Program Files: Windows pide una vez el permiso de un administrador.
spanish.ModeOnlyMe=Solo para mí (%1)
spanish.ModeEveryone=Para todos
spanish.ModeRefused=Windows no concedió permisos de administrador (código %1), y sin ellos no se puede instalar Voicer para todos.%n%nEjecuta la instalación como administrador para instalar para todos: clic derecho en el archivo de instalación → «Ejecutar como administrador». O elige «Solo para mí».
spanish.DeleteDataPrompt=¿Borrar también los datos de Voicer (modelos descargados, FFmpeg, ajustes) de la carpeta de abajo?
spanish.DeleteDataHint=Elige «No» si vas a volver a instalar Voicer: los modelos ocupan varios gigabytes.
french.DesktopIcon=Raccourci sur le bureau
french.AdditionalTasks=Options supplémentaires :
french.LaunchVoicer=Lancer Voicer
french.ModeTitle=Pour qui installer Voicer
french.ModeQuestion=Qui utilisera le programme sur cet ordinateur ?
french.ModeHelp=« Seulement pour moi » installe le programme dans votre profil et ne demande pas de droits d'administrateur.%n« Pour tout le monde » l'installe dans Program Files : Windows demande une fois l'autorisation d'un administrateur.
french.ModeOnlyMe=Seulement pour moi (%1)
french.ModeEveryone=Pour tout le monde
french.ModeRefused=Windows n'a pas accordé les droits d'administrateur (code %1), et sans eux Voicer ne peut pas être installé pour tout le monde.%n%nLancez l'installation en tant qu'administrateur pour installer pour tout le monde : clic droit sur le fichier d'installation → « Exécuter en tant qu'administrateur ». Ou choisissez « Seulement pour moi ».
french.DeleteDataPrompt=Supprimer aussi les données de Voicer (modèles téléchargés, FFmpeg, réglages) du dossier ci-dessous ?
french.DeleteDataHint=Choisissez « Non » si vous comptez réinstaller Voicer : les modèles pèsent plusieurs gigaoctets.
portuguese.DesktopIcon=Atalho no ambiente de trabalho
portuguese.AdditionalTasks=Adicional:
portuguese.LaunchVoicer=Iniciar o Voicer
portuguese.ModeTitle=Para quem instalar o Voicer
portuguese.ModeQuestion=Quem vai usar o programa neste computador?
portuguese.ModeHelp=“Só para mim” instala o programa no seu perfil e não precisa de direitos de administrador.%n“Para todos” instala-o em Program Files: o Windows pede uma vez a permissão de um administrador.
portuguese.ModeOnlyMe=Só para mim (%1)
portuguese.ModeEveryone=Para todos
portuguese.ModeRefused=O Windows não concedeu direitos de administrador (código %1), e sem eles não é possível instalar o Voicer para todos.%n%nExecute a instalação como administrador para instalar para todos: clique com o botão direito no ficheiro de instalação → “Executar como administrador”. Ou escolha “Só para mim”.
portuguese.DeleteDataPrompt=Eliminar também os dados do Voicer (modelos transferidos, FFmpeg, definições) da pasta abaixo?
portuguese.DeleteDataHint=Escolha «Não» se vai voltar a instalar o Voicer: os modelos ocupam vários gigabytes.
russian.DesktopIcon=Значок на рабочем столе
russian.AdditionalTasks=Дополнительно:
russian.LaunchVoicer=Запустить Voicer
russian.ModeTitle=Для кого установить Voicer
russian.ModeQuestion=Кто будет пользоваться программой на этом компьютере?
russian.ModeHelp=«Только для меня» ставит программу в ваш профиль и не требует прав администратора.%n«Для всех» ставит её в Program Files: Windows один раз попросит разрешение администратора.
russian.ModeOnlyMe=Только для меня (%1)
russian.ModeEveryone=Для всех
russian.ModeRefused=Windows не выдала права администратора (код %1), а без них установить Voicer для всех нельзя.%n%nЗапустите установку от имени администратора, чтобы установить для всех: правый щелчок по файлу установки → «Запуск от имени администратора». Или выберите «Только для меня».
russian.DeleteDataPrompt=Удалить и данные Voicer (скачанные модели, FFmpeg, настройки) из папки ниже?
russian.DeleteDataHint=Нажмите «Нет», если собираетесь поставить Voicer снова: модели весят несколько гигабайт.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalTasks}"; Flags: unchecked

[Files]
; Everything from the published package except the data of a copy that was tried out from it.
Source: "{#VoicerSource}\*"; DestDir: "{app}"; Excludes: "Data\*,Data,\voicer.portable"; Flags: recursesubdirs createallsubdirs ignoreversion
; The marker only for the current user: in Program Files the data goes to the profile of each user.
Source: "{#VoicerSource}\voicer.portable"; DestDir: "{app}"; Check: not IsAdminInstallMode; Flags: ignoreversion

[Icons]
Name: "{group}\Voicer"; Filename: "{app}\Voicer.exe"; IconFilename: "{app}\Voicer.exe"
Name: "{autodesktop}\Voicer"; Filename: "{app}\Voicer.exe"; IconFilename: "{app}\Voicer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Voicer.exe"; Description: "{cm:LaunchVoicer}"; Flags: nowait postinstall skipifsilent

[Code]
{ Starts a program through the shell; with the verb "runas" Windows asks for administrator rights. Returns a number
  above 32 on success, otherwise an error code (5 - refused, 2 - file not found...). }
function ShellExecuteW(Wnd: HWND; Operation, FileName, Parameters, Directory: String; ShowCmd: Integer): THandle;
  external 'ShellExecuteW@shell32.dll stdcall';

var
  { The page "whom to install for". }
  ModePage: TInputOptionWizardPage;
  { Setup was started again with administrator rights; this copy closes without questions. }
  Relaunched: Boolean;

{ An installation that is already there keeps the way it was installed. }
function IsUpdate(): Boolean;
begin
  Result := WizardForm.PrevAppDir <> '';
end;

{ Adds the page "whom to install for" right after the greeting. }
procedure InitializeWizard();
begin
  ModePage := CreateInputOptionPage(wpWelcome,
    CustomMessage('ModeTitle'), CustomMessage('ModeQuestion'), CustomMessage('ModeHelp'), True, False);
  ModePage.Add(FmtMessage(CustomMessage('ModeOnlyMe'), [ExpandConstant('{username}')]));
  ModePage.Add(CustomMessage('ModeEveryone'));
  if IsAdminInstallMode then
    ModePage.SelectedValueIndex := 1
  else
    ModePage.SelectedValueIndex := 0;

  { On screens scaled above 100% the boxes of the lists are drawn larger while their distance from the edge stays
    the same, so the left side of a box is cut off. The distance is scaled with the rest of the wizard. }
  ModePage.CheckListBox.Offset := ScaleX(8);
  WizardForm.TasksList.Offset := ScaleX(8);
  WizardForm.RunList.Offset := ScaleX(8);
end;

{ The copy started with /ALLUSERS has the answer already, and an update does not ask again. }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = ModePage.ID) and (IsAdminInstallMode or IsUpdate());
end;

{ Installing for everyone needs administrator rights from the very start: Setup is started again with them, in the
  language already chosen, and this copy closes. }
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Code: THandle;
begin
  Result := True;
  if (CurPageID = ModePage.ID) and (ModePage.SelectedValueIndex = 1) and not IsAdminInstallMode then
  begin
    Result := False;
    Code := ShellExecuteW(WizardForm.Handle, 'runas', ExpandConstant('{srcexe}'), '/ALLUSERS /LANG=' + ActiveLanguage, '', SW_SHOWNORMAL);
    if Code > 32 then
    begin
      Relaunched := True;
      WizardForm.Close;
    end
    else
      MsgBox(FmtMessage(CustomMessage('ModeRefused'), [IntToStr(Code)]), mbInformation, MB_OK);
  end;
end;

{ The copy that handed the work to the one with administrator rights closes without asking "Exit Setup?". }
procedure CancelButtonClick(CurPageID: Integer; var Cancel, Confirm: Boolean);
begin
  if Relaunched then
    Confirm := False;
end;

{ After the program is removed, offers to delete its data as well: from the installation for the current user, from
  the profile of the user who removes it for all users (the profiles of the others are not touched). }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if IsAdminInstallMode then
      DataDir := ExpandConstant('{localappdata}\Voicer\Data')
    else
      DataDir := ExpandConstant('{app}\Data');
    if DirExists(DataDir) then
    begin
      if MsgBox(ExpandConstant('{cm:DeleteDataPrompt}')
        + #13#10 + #13#10 + DataDir + #13#10 + #13#10
        + ExpandConstant('{cm:DeleteDataHint}'), mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
