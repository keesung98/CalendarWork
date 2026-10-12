; CalendarWork 설치 프로그램 (Inno Setup 6)
; 직접 빌드하지 말고 build\package.ps1 을 쓴다 (게시 → 버전 전달 → 컴파일).
;   iscc /DAppVersion=0.1.0 /DPublishDir=..\artifacts\publish\win-x64 installer\CalendarWork.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef AppPublisher
  #define AppPublisher "kschoi"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

#define AppName "CalendarWork"
#define AppExeName "CalendarWork.exe"
; 앱의 StartupRegistration과 같은 값 이름을 써서, 설정 창의 "자동 실행"과 설치 옵션이 같은 항목을 가리키게 한다
#define RunValueName "CalendarWork"

[Setup]
; AppId는 업그레이드·제거 식별자이므로 절대 바꾸지 않는다
AppId={{7B3E2F4A-9C1D-4E8B-A6F2-3D5C8E1B9A47}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} 설치 프로그램
VersionInfoCompany={#AppPublisher}

; 사용자 단위 설치(관리자 권한 불필요): %LocalAppData%\Programs\CalendarWork
; 설치 화면에서 "모든 사용자"를 고르면 관리자 권한으로 Program Files에 설치한다
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

OutputDir=..\artifacts\installer
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=..\src\CalendarWork.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; 실행 중인 앱은 [Code]에서 직접 종료한다 (Restart Manager는 트레이 상주 앱을 깔끔히 닫지 못함)
CloseApplications=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "startup"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 옵션:"
Name: "desktopicon"; Description: "바탕화면에 바로 가기 만들기"; GroupDescription: "추가 옵션:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; 업그레이드일 때만, 이전 버전에서 빠진 파일이 남지 않도록 프로그램 폴더를 비운다
; (처음 설치 때는 사용자가 고른 폴더의 다른 파일을 지우지 않도록 건너뜀. 사용자 데이터는 %LocalAppData%\CalendarWork에 따로 있음)
Type: filesandordirs; Name: "{app}\*"; Check: IsUpgrade

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; 자동 실행 선택 시 등록. 선택하지 않으면 개발 빌드 등 이전 값이 남지 않도록 지운다
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#RunValueName}"; ValueData: """{app}\{#AppExeName}"""; Tasks: startup; Flags: uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#RunValueName}"; Tasks: not startup; Flags: deletevalue uninsdeletevalue

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{#AppName} 실행"; Flags: nowait postinstall skipifsilent

[Code]
const
  UserDataDirName = 'CalendarWork';
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{7B3E2F4A-9C1D-4E8B-A6F2-3D5C8E1B9A47}_is1';

{ 같은 AppId로 이미 설치되어 있으면 업그레이드 }
function IsUpgrade(): Boolean;
begin
  Result := RegKeyExists(HKCU, UninstallKey) or RegKeyExists(HKLM, UninstallKey);
end;

{ 설치·업그레이드·제거 전에 실행 중인 CalendarWork를 종료한다 }
procedure CloseRunningApp();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExeName} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseRunningApp();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  CloseRunningApp();
  Result := True;
end;

{ 제거가 끝나면 메모·할일 데이터도 지울지 묻는다 (기본: 남김). 자동(silent) 제거에서는 남긴다 }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep <> usPostUninstall then
    Exit;

  DataDir := ExpandConstant('{localappdata}\') + UserDataDirName;
  if not DirExists(DataDir) or UninstallSilent() then
    Exit;

  if MsgBox('메모·할일·설정 데이터도 삭제할까요?' + #13#10 + #13#10 +
            '다시 설치할 때 데이터를 쓰려면 [아니요]를 누르세요.' + #13#10 + DataDir,
            mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    DelTree(DataDir, True, True, True);
end;
