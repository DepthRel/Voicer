; Inno Setup script of Voicer. The wizard asks whether to install for the current user or for all users:
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
; The current user is the default; "all users" in the dialog restarts the wizard with administrator rights. An update
; keeps the mode of the installation it replaces.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
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
; Everything the wizard says beyond the standard messages of Inno Setup.
english.DesktopIcon=Desktop shortcut
english.AdditionalTasks=Additional tasks:
english.LaunchVoicer=Start Voicer
english.DeleteDataPrompt=Also delete the data of Voicer (downloaded models, FFmpeg, settings) from the folder below?
english.DeleteDataHint=Choose "No" if you are going to install Voicer again: the models are several gigabytes.
german.DesktopIcon=Verknüpfung auf dem Desktop
german.AdditionalTasks=Zusätzlich:
german.LaunchVoicer=Voicer starten
german.DeleteDataPrompt=Auch die Daten von Voicer (heruntergeladene Modelle, FFmpeg, Einstellungen) aus dem Ordner unten löschen?
german.DeleteDataHint=Wählen Sie „Nein“, wenn Sie Voicer erneut installieren wollen: Die Modelle sind mehrere Gigabyte groß.
spanish.DesktopIcon=Acceso directo en el escritorio
spanish.AdditionalTasks=Adicional:
spanish.LaunchVoicer=Iniciar Voicer
spanish.DeleteDataPrompt=¿Borrar también los datos de Voicer (modelos descargados, FFmpeg, ajustes) de la carpeta de abajo?
spanish.DeleteDataHint=Elige «No» si vas a volver a instalar Voicer: los modelos ocupan varios gigabytes.
french.DesktopIcon=Raccourci sur le bureau
french.AdditionalTasks=Options supplémentaires :
french.LaunchVoicer=Lancer Voicer
french.DeleteDataPrompt=Supprimer aussi les données de Voicer (modèles téléchargés, FFmpeg, réglages) du dossier ci-dessous ?
french.DeleteDataHint=Choisissez « Non » si vous comptez réinstaller Voicer : les modèles pèsent plusieurs gigaoctets.
portuguese.DesktopIcon=Atalho no ambiente de trabalho
portuguese.AdditionalTasks=Adicional:
portuguese.LaunchVoicer=Iniciar o Voicer
portuguese.DeleteDataPrompt=Eliminar também os dados do Voicer (modelos transferidos, FFmpeg, definições) da pasta abaixo?
portuguese.DeleteDataHint=Escolha «Não» se vai voltar a instalar o Voicer: os modelos ocupam vários gigabytes.
russian.DesktopIcon=Значок на рабочем столе
russian.AdditionalTasks=Дополнительно:
russian.LaunchVoicer=Запустить Voicer
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
{ On screens scaled above 100% the boxes of the lists are drawn larger while their distance from the edge stays the
  same, so the left side of a box is cut off. The distance is scaled with the rest of the wizard. }
procedure InitializeWizard();
begin
  WizardForm.TasksList.Offset := ScaleX(8);
  WizardForm.RunList.Offset := ScaleX(8);
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
