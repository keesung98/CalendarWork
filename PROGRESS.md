# CalendarWork 작업 내역

마지막 업데이트: 2026-10-12 · 현재 버전 **1.0.0** · 만든이 kschoi

바탕화면 달력·할일 위젯 (WPF). 원 설계는 [design.md](design.md). 이 문서는 **지금 구현된 상태**와 **다음에 이어서 할 일**을 정리한 것이다. 설계서와 다른 부분은 이 문서가 기준이다.

---

## 1. 빠른 시작

| 작업 | 명령 |
|---|---|
| 실행 (개발) | `dotnet run --project src/CalendarWork.App` |
| 테스트 | `dotnet test` (Core 34개 + Data 6개) |
| 설치 프로그램 만들기 | `powershell -ExecutionPolicy Bypass -File build\package.ps1` → `artifacts\installer\CalendarWork-Setup-<버전>.exe` |
| 실제 데이터와 분리해 실행 | 환경 변수 `CALENDARWORK_DATA_DIR=<임시 폴더>` 설정 후 실행 |

- 환경: Windows 11 25H2, .NET SDK 9.0.300, Inno Setup 6.7.3 (winget, 사용자 범위 설치)
- 버전·게시자 변경: `src/CalendarWork.App/CalendarWork.App.csproj`의 `<Version>`, `<Company>`만 고치면 앱 설정 창과 설치 프로그램에 함께 반영된다.
- 사용자 데이터: `%LocalAppData%\CalendarWork\` (`calendar.db`, `settings.json`, `holidays\`, 오류 시 `error.log`)
- 단일 실행: 이미 실행 중이면 새로 띄운 앱은 바로 종료된다 (설치판이 떠 있으면 개발판 테스트 불가 → 설치판을 먼저 종료).

---

## 2. 솔루션 구조

```
CalendarWork.sln
├─ src/
│  ├─ CalendarWork.Core/      순수 C#: 42칸 그리드, 음력 변환, 공휴일, 반복 일정 계산, 모델
│  ├─ CalendarWork.Data/      SQLite(WAL) 저장소, 마이그레이션(user_version), JSON 백업
│  ├─ CalendarWork.Interop/   Win32: 바탕화면 부착, 클릭 투과, 트레이 아이콘, 전역 단축키
│  └─ CalendarWork.App/       WPF (MVVM)
│     ├─ App.xaml(.cs)        컴포지션 루트: DI 등록, ViewModelLocator, 트레이 메뉴
│     ├─ DesktopHost.cs       창 수명·잠금/편집 반영·트레이·단축키·탐색기 재시작 대응
│     ├─ Services/            설정, 경로, 시계(자정 갱신), 공휴일 갱신, 테마, 대화상자, 셸
│     ├─ ViewModels/          Main, Settings, 편집기들, ViewModelLocator
│     ├─ Views/               창 XAML, Behaviors(첨부 속성), 변환기, ImeSafeCommand
│     ├─ Themes/              Dark/Light(브러시 Freeze), Controls(스타일·변환기 리소스)
│     └─ Properties/PublishProfiles/win-x64.pubxml   배포용 게시 설정
├─ tests/  CalendarWork.Core.Tests, CalendarWork.Data.Tests (xUnit)
├─ installer/CalendarWork.iss  Inno Setup 스크립트
├─ build/package.ps1           테스트 → 게시 → 설치 프로그램
└─ artifacts/                  빌드 결과 (git 제외)
```

의존 방향: `App → Core, Data, Interop` / `Data → Core` / `Core`는 아무것도 참조하지 않음.

---

## 3. 구현된 기능

### 달력
- 월간 6주×7일 고정 그리드, 셀 ViewModel 42개 재사용 (월 이동 시 값만 갱신)
- 한국 공휴일·대체공휴일·임시공휴일 (2026·2027년 내장 JSON) + 원격 갱신 코드(주소 비어 있어 꺼짐)
- 음력 표시: 양력 1일·음력 초하루·보름만 / 매일 / 끄기
- 일요일·공휴일 빨강, 토요일 파랑, 오늘 강조, 이전/다음 달 흐리게
- 날짜 칸 마우스 오버 → 전체 내용 미리보기 (공휴일·음력·반복 일정·메모·할일)
- 이동: ◀ 오늘 ▶ 버튼, 마우스 휠, Alt+←/→/Home
- 자정·시계 변경·절전 복귀·잠금 해제 시 "오늘" 갱신 (폴링 없음)

### 메모
- 날짜 칸 더블클릭 → 인라인 편집기, 여러 줄, 색상 4종(●로 순환), Ctrl+Enter 저장 / Esc 취소
- 칸에는 최대 3줄 + "외 N건"

### 할일
- 추가(마감일 기본값 = 오늘, 지우면 마감 없음), 체크, 삭제
- 마우스 오버 시 ▲▼(순서, 미완료/완료 그룹 안에서만) ✎(인라인 수정) ✕
- 마감일 있는 미완료 할일은 달력 칸에 `☐ 내용`으로 표시

### D-day (1개)
- 양력 하루짜리, 또는 **음력으로 매년 반복**(생일·제사) → 헤더에 `D-338 추석 (음 8.15)`

### 반복 일정 (설계서상 Pro, 현재 제한 없이 사용 가능)
- 매주 / 매월 / 매년 / 음력 매년, 색상 4종, 헤더 ↻ 또는 메모 편집기의 "↻ 반복 일정"에서 등록
- 없는 날짜 대체: 매월 31일→말일, 2/29→평년 2/28, 음력 윤달→평달, 음력 30일→29일

### 바탕화면 위젯
- **잠금 모드**: 바탕화면에 고정 + 클릭 투과(아이콘 클릭 가능), Win+D에도 유지, 오른쪽 위 🔒 버튼으로 해제
- **편집 모드**: 점선 테두리, 빈 곳(날짜 칸 포함)을 끌어 이동, 오른쪽 아래 손잡이로 크기 조절
- 전환: Ctrl+Alt+D, 트레이 아이콘 클릭/메뉴, 🔒 버튼, 편집 모드에서 Ctrl+클릭
- 할일 패널 토글: Ctrl+Alt+T
- 트레이 메뉴: 모드 전환, 오늘로 이동, 할일 패널, 설정, 종료

### 설정 창
- 배경 불투명도, 다크/라이트 테마, 음력 표시, 부착 방식, 자동 실행, 공휴일 갱신 주소, 단축키 안내
- JSON 내보내기/가져오기(메모·할일·D-day·반복 일정, 가져오기는 전체 교체)
- 하단에 버전·만든이 표시. 변경 즉시 적용·저장

### 배포
- Inno Setup 설치 프로그램 (42.5MB, .NET 런타임 포함, x64, ReadyToRun)
- 관리자 권한 없이 `%LocalAppData%\Programs\CalendarWork`에 설치 (원하면 모든 사용자 설치)
- 한국어 마법사, 시작 메뉴, 자동 실행(기본 체크), 바탕화면 바로 가기(선택)
- 업그레이드 시 실행 중인 앱 종료 후 교체, 제거 시 데이터 삭제 여부 질문(기본: 남김)
- `AppId {7B3E2F4A-9C1D-4E8B-A6F2-3D5C8E1B9A47}`은 업그레이드 식별자 → **절대 변경 금지**

---

## 4. 설계서와 달라진 결정

| 항목 | 설계서 | 현재 | 이유 |
|---|---|---|---|
| 런타임 | .NET 10 | .NET 9 (`net9.0-windows`) | 설치된 SDK가 9뿐. SDK 설치 후 TargetFramework만 바꾸면 됨 |
| 잠금 모드 부착 | WorkerW 자식(아이콘 뒤) | **Progman 소유 창**(기본) + 클릭 투과 | Win11 24H2+(25H2 실기)에서 WorkerW·Progman 자식으로 붙이면 WPF 창이 아예 그려지지 않음(불투명 창도 동일, 실험으로 확인). 소유 창은 Win+D에도 유지되지만 아이콘 **위**에 그려짐. WorkerW·최하단 창은 설정에서 선택 가능 |
| 잠금 해제 | 트레이·단축키·Ctrl+클릭 | + 달력 오른쪽 위 🔒 버튼(별도 작은 창) | 잠금 중엔 달력이 클릭 투과라 바탕화면에서 풀 방법이 필요 |
| 패키징 | MSIX (Store) | Inno Setup setup.exe | 사용자 선택(직접 배포). MSIX는 추후 |
| 자동 실행 | MSIX StartupTask | HKCU Run 키 (`CalendarWork` 값) | MSIX 미사용. 앱 설정과 설치 프로그램이 같은 값을 공유 |
| 트레이 | — | Shell_NotifyIcon 직접 호출 (설계대로 WinForms 미사용) | |

---

## 5. 코드 규칙 (유지할 것)

- **엄격한 MVVM**: `.xaml.cs`에는 `InitializeComponent()` 생성자만. DataContext는 XAML에서
  `DataContext="{Binding Main, Source={StaticResource Locator}}"` (ViewModelLocator + DI `Ioc.Default`).
- 이벤트가 필요하면 `Views/Behaviors`의 첨부 속성 사용:
  `WindowBehaviors`(DragMoveEnabled, CtrlClickCommand, HideFromAltTab, CloseOnRequest), `FocusBehavior.IsFocused`, `MouseWheelBehavior`.
- ViewModel에는 WPF 타입 금지 (색은 번호 → `MemoColorToBrush` 변환기). 대화상자·창·테마·자동 실행은 `Services/ServiceInterfaces.cs` 인터페이스로 주입.
- 새 ViewModel/서비스는 `App.xaml.cs`의 `ConfigureServices`와 `ViewModelLocator`에 등록.
- 잠금/편집 상태의 기준은 `MainViewModel.IsEditMode` 하나. `DesktopHost`는 반영만 한다.
- 텍스트 입력 후 저장하는 명령은 `Converter={StaticResource ImeSafe}`로 감싼다 (한글 조합 중 마지막 글자 누락 방지). 이런 명령은 CanExecute로 버튼을 막지 말고 실행 시 빈 값 검사.
- 트레이 메뉴처럼 `Locator`를 StaticResource로 참조하는 리소스는 App.xaml에 직접 둔다 (병합 사전에서는 참조 실패).
- 성능 원칙(설계서 5.2): 애니메이션·그림자·블러 금지, 브러시 Freeze, 폴링 타이머 금지.

---

## 6. 수정 이력 (2026-10-12)

1. **초기 구현**: 솔루션 5개 프로젝트 + 테스트, 달력·공휴일·음력·메모·할일·D-day·트레이·단축키·설정·테마·자동 실행·백업
2. **부착 방식 변경**: WorkerW가 24H2+에서 안 그려지는 문제 → Progman 소유 창 기본값
3. **편집 모드 창 이동**: 헤더만 → 빈 곳 어디서나 드래그
4. **버그 수정**
   - 한글 입력 후 저장 시 마지막 글자 누락("시험"→"시") → `ImeSafeCommand`
   - 창을 한 번도 옮기지 않으면 설정 저장 시 예외(위치 NaN 직렬화) → NaN 허용 + 시작 시 위치 기록
   - 비활성 창 Ctrl+클릭 잠금 미동작 → 실제 Ctrl 키 상태 확인(`KeyboardState`)
5. **기능 추가**: 바탕화면 🔒 잠금 해제 버튼, 음력 D-day, 할일 수정·순서, 날짜 미리보기, 반복 일정, 할일 마감일 기본값 오늘
6. **MVVM 리팩토링**: 코드 비하인드 제거, XAML 바인딩, DI, Behaviors, ViewModel에서 WPF 타입 제거
7. **정리**: 미사용 파일(AssemblyInfo.cs)·리소스·코드 삭제
8. **배포**: 게시 프로필, Inno Setup 스크립트, `build/package.ps1`
9. **버전 1.0.0**, 설정 창 하단 버전·만든이(kschoi), 설치 프로그램 게시자 kschoi

---

## 7. 남은 일 / 알려진 문제

### 출시 전 필요
- [ ] **메모리**: 약 150MB (목표 60MB 이하). Release·ReadyToRun 기준 측정 후 원인 분석 필요
- [ ] **코드 서명**: 없으면 다운로드 시 SmartScreen 경고
- [ ] **공휴일 데이터 검증**: 2026년 제헌절(7/17)·노동절(5/1) 공휴일 지정 여부 미확인으로 미포함. 6/3 지방선거는 포함. 2028년 이후 데이터 없음
- [ ] **공휴일 원격 갱신 주소**: 호스팅 위치 미정(GitHub Pages 권장). 형식: `manifest.json` `{ "files": [{ "year", "version", "path" }] }` + `holidays-YYYY.json`
- [ ] [design.md](design.md) 5.1절을 실제 부착 방식으로 갱신

### 기능 (설계서 MVP/Pro 중 미구현)
- [ ] Pro 라이선스(Store add-on) — 지금은 반복 일정이 제한 없이 열려 있음
- [ ] 다중 모니터별 달력, 테마 팩, 주간/2개월 보기, 구글 캘린더 연동
- [ ] D-day 여러 개(Pro), 트레이 "후원하기"(링크 미정)
- [ ] MSIX 패키징·Store 제출(필요 시 자동 실행을 StartupTask로 교체)

### 개선 후보
- [ ] ViewModel 단위 테스트 프로젝트 (ViewModel이 WPF 독립이라 가능. 단 `ClockService`는 인터페이스화 필요)
- [ ] 주 시작 요일 선택(일/월) — `CalendarGrid`는 이미 지원
- [ ] 글자 크기 조절, 오늘 할일 요약
- [ ] 잠금 모드에서 달력이 아이콘 위에 그려지는 문제 (24H2+ 구조 한계, 대안 조사 필요)

---

## 8. 테스트 방법 메모

- 단위 테스트: `dotnet test`
- UI 확인은 실제 데이터를 건드리지 않도록 `CALENDARWORK_DATA_DIR`로 임시 폴더를 지정해 실행
- 자동화로 UI를 검증할 때는 UI Automation 패턴(Invoke/Value/Toggle)만 쓰고 키보드 입력은 보내지 않는다
  (클릭이 빗나가면 다른 창에 입력됨). 입력칸에는 `x:Name`/`AutomationId`가 붙어 있다
  (`NewTodoBox`, `DDayTitleBox`, `DDayDatePicker`, `RecurrenceTitleBox`, `RecurrenceRuleCombo`, `RecurrenceDatePicker`, `TodoEditBox` 등)
- 설치 프로그램 검증: `/VERYSILENT /CURRENTUSER /DIR=<임시> /TASKS=startup`로 설치 → 실행 → 재설치(업그레이드) → `unins000.exe /VERYSILENT`로 제거.
  자동 실행 레지스트리 값(`HKCU\...\Run\CalendarWork`)을 미리 백업했다가 복원할 것
