; Script de instalaci�n de Traductor MSM
; Generado para Inno Setup 6

#define MyAppName "Traductor MSM"
#define MyAppVersion "1.6.0"
#define MyAppPublisher "MSM"
#define MyAppExeName "Traductor MSM.exe"
#define MyAppAssocName MyAppName + " File"
#define MyAppAssocExt ".msm"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
; Informaci�n b�sica de la aplicaci�n
AppId={{B8F5A3D2-7E4C-4B9A-8F2D-1C5E6A3B9D7F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Icono del instalador
SetupIconFile=C:\Users\j-b-j\Documents\Traductor\icon.ico
; Archivo de salida
OutputDir=C:\Users\j-b-j\Documents\Traductor\Installer
OutputBaseFilename=TraductorMSM_Setup_v{#MyAppVersion}
; Compresi�n
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Permisos
PrivilegesRequired=lowest
; Arquitectura
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "quicklaunchicon"; Description: "Crear icono en inicio r�pido"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Ejecutable principal
Source: "C:\Users\j-b-j\Documents\Traductor\bin\Release\net8.0-windows\Traductor MSM.exe"; DestDir: "{app}"; Flags: ignoreversion
; DLLs necesarias
Source: "C:\Users\j-b-j\Documents\Traductor\bin\Release\net8.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\j-b-j\Documents\Traductor\bin\Release\net8.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion
; Runtime de .NET si est� presente
Source: "C:\Users\j-b-j\Documents\Traductor\bin\Release\net8.0-windows\*.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
; Icono
Source: "C:\Users\j-b-j\Documents\Traductor\icon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Icono en el men� de inicio
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
; Icono en el escritorio
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\icon.ico"; Tasks: desktopicon
; Icono en inicio r�pido
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
; Ejecutar la aplicaci�n despu�s de la instalaci�n
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Verificar si .NET 8.0 est� instalado
function IsDotNetInstalled: Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNetInstalled then
  begin
    if MsgBox('Esta aplicaci�n requiere .NET 8.0 Runtime.' + #13#10 +
              '�Desea abrir la p�gina de descarga de .NET 8.0?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOW, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;
