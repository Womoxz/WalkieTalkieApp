; Instalador de Walkie Talkie VW - Inno Setup 6
; Compilar con:  "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\WalkieTalkie.iss

#define AppName        "Walkie Talkie VW"
#define AppVersion     "2.2.0"
#define AppPublisher   "TuEmpresa"
#define AppExe         "WalkieTalkieApp.exe"
#define SourceDir      "..\bin\Release\net9.0-windows\win-x64\publish"

[Setup]
AppId={{9F3C1E64-8B7A-4D52-A1F3-2C6E4B8D5A70}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}

; Se instala en C:\WalkieTalkie, no en Archivos de programa: la aplicación guarda
; ahí mismo su configuración, el usuario y el historial de audios, y en Archivos de
; programa un usuario sin permisos de administrador no podría escribir.
DefaultDirName=C:\WalkieTalkie
DisableDirPage=no
DefaultGroupName={#AppName}
AllowNoIcons=yes

OutputDir=dist
OutputBaseFilename=WalkieTalkieVW_{#AppVersion}_Setup
SetupIconFile=..\resources\VW_Talk_Logo.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Si la aplicación está abierta, el instalador ofrece cerrarla en vez de fallar.
CloseApplications=yes
CloseApplicationsFilter=*.exe
AppMutex=Global\WalkieTalkieApp.SingleInstance

[Languages]
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "startup";     Description: "Iniciar automáticamente al encender el equipo"; GroupDescription: "Opciones:"; Flags: checkedonce
Name: "firewall";    Description: "Permitir la aplicación en el Firewall de Windows (recomendado)"; GroupDescription: "Opciones:"

[Files]
Source: "{#SourceDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

; La configuración NO se sobrescribe al actualizar: contiene los contactos
; descubiertos y los ajustes de cada equipo.
Source: "{#SourceDir}\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

Source: "{#SourceDir}\resources\*"; DestDir: "{app}\resources"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceDir}\sounds\*";    DestDir: "{app}\sounds";    Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Permisos de escritura para todos los usuarios: la app guarda aquí el historial,
; el usuario de la sesión y la configuración.
Name: "{app}";        Permissions: users-modify
Name: "{app}\audios"; Permissions: users-modify

[Icons]
Name: "{group}\{#AppName}";                  Filename: "{app}\{#AppExe}"; IconFilename: "{app}\resources\VW_Talk_Logo.ico"
Name: "{group}\Desinstalar {#AppName}";      Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";            Filename: "{app}\{#AppExe}"; IconFilename: "{app}\resources\VW_Talk_Logo.ico"; Tasks: desktopicon

[Registry]
; El arranque automático se registra para el equipo, no para la cuenta que instala:
; si un técnico instala con su usuario de administrador, un acceso directo en la
; carpeta de Inicio se crearía en el perfil equivocado y no arrancaría a nadie.
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "WalkieTalkieVW"; ValueData: """{app}\{#AppExe}"""; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
; Reglas de firewall para la voz (5000) y la búsqueda de equipos (5001).
; Sin esto, Windows muestra un aviso al primer arranque que un usuario sin permisos
; de administrador no puede aceptar, y el walkie-talkie se queda mudo.
Filename: "{sys}\netsh.exe"; \
    Parameters: "advfirewall firewall add rule name=""Walkie Talkie VW (UDP entrante)"" dir=in action=allow protocol=UDP localport=5000-5001 program=""{app}\{#AppExe}"" enable=yes profile=any"; \
    Flags: runhidden waituntilterminated; Tasks: firewall; StatusMsg: "Configurando el Firewall de Windows..."

Filename: "{sys}\netsh.exe"; \
    Parameters: "advfirewall firewall add rule name=""Walkie Talkie VW (UDP saliente)"" dir=out action=allow protocol=UDP localport=5000-5001 program=""{app}\{#AppExe}"" enable=yes profile=any"; \
    Flags: runhidden waituntilterminated; Tasks: firewall

Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName} ahora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Walkie Talkie VW (UDP entrante)"""; Flags: runhidden; RunOnceId: "DelFwIn"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Walkie Talkie VW (UDP saliente)"""; Flags: runhidden; RunOnceId: "DelFwOut"

[Code]
// Al desinstalar se pregunta qué hacer con las grabaciones y la configuración,
// en lugar de borrarlas sin avisar o dejar basura para siempre.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Respuesta: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if DirExists(ExpandConstant('{app}\audios')) then
    begin
      Respuesta := MsgBox('¿Quieres borrar también los audios grabados y la configuración?' + #13#10 + #13#10 +
                          'Elige No si vas a volver a instalar la aplicación.',
                          mbConfirmation, MB_YESNO);
      if Respuesta = IDYES then
      begin
        DelTree(ExpandConstant('{app}\audios'), True, True, True);
        DeleteFile(ExpandConstant('{app}\appsettings.json'));
        DeleteFile(ExpandConstant('{app}\user.txt'));
      end;
    end;
  end;
end;
