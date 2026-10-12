# CalendarWork

Windows 바탕화면에 붙어 있는 달력·할일 위젯입니다.
한국 공휴일·대체공휴일·음력·D-day를 보여 주고, 날짜 칸에서 바로 메모와 할일을 관리합니다. 광고는 없습니다.

- 버전: 1.0.0
- 만든이: kschoi
- 지원 OS: Windows 10 1809 이상 / Windows 11 (x64)

## 주요 기능

- **월간 달력**: 6주 고정 그리드, 일요일·공휴일 빨강, 토요일 파랑, 오늘 강조
- **한국 공휴일**: 대체공휴일·임시공휴일 포함 (2026–2027년 내장)
- **음력**: 초하루·보름만 또는 매일 표시
- **메모**: 날짜 칸을 더블클릭해 바로 입력, 여러 줄, 색상 4종
- **할일**: 마감일을 지정하면 달력에 표시, 수정·순서 변경
- **D-day**: 양력 날짜, 또는 음력으로 매년 반복 (생일·제사)
- **반복 일정**: 매주 / 매월 / 매년 / 음력 매년
- **날짜 미리보기**: 마우스를 올리면 그날의 일정 전체 표시
- **바탕화면 고정**: 잠금 모드에서는 클릭이 바탕화면 아이콘으로 통과하고, Win+D를 눌러도 남아 있음
- **다크 / 라이트 테마**, 배경 투명도 조절
- **데이터 내보내기·가져오기** (JSON)

## 설치

1. [Releases](../../releases)에서 `CalendarWork-Setup-<버전>.exe`를 내려받아 실행합니다.
   - .NET 런타임이 포함되어 있어 따로 설치할 것이 없습니다.
   - 관리자 권한도 필요 없습니다.
2. 코드 서명이 없어서 "Windows의 PC 보호" 경고가 뜰 수 있습니다. **추가 정보 → 실행**을 누르세요.

제거는 Windows 설정 → 앱에서 할 수 있습니다. 제거할 때 메모·할일 데이터를 남길지 고를 수 있습니다.

## 사용법

처음 실행하면 **편집 모드**입니다. 달력을 원하는 위치로 옮기고 크기를 맞춘 뒤 **잠금 모드**로 바꾸세요.

| 동작 | 방법 |
|---|---|
| 잠금 ↔ 편집 전환 | `Ctrl+Alt+D`, 트레이 아이콘 클릭 |
| 잠금 해제 (바탕화면에서) | 달력 오른쪽 위 🔒 버튼 |
| 잠금 (편집 모드에서) | 🔒 버튼 또는 `Ctrl+클릭` |
| 할일 패널 열기/닫기 | `Ctrl+Alt+T` |
| 창 이동 / 크기 조절 | 편집 모드에서 빈 곳을 끌기 / 오른쪽 아래 손잡이 |
| 메모 쓰기 | 날짜 칸 더블클릭 → `Ctrl+Enter` 저장, `Esc` 취소 |
| 달 이동 | ◀ ▶ 버튼, 마우스 휠, `Alt+←/→`, `Alt+Home`(오늘) |
| 설정 | 편집 모드의 ⚙ 버튼, 트레이 우클릭 메뉴 |

데이터는 `%LocalAppData%\CalendarWork`에 저장됩니다.

## 소스에서 빌드

필요한 것:
- Windows
- [.NET SDK 9](https://dotnet.microsoft.com/download)
- (설치 프로그램을 만들 때만) [Inno Setup 6](https://jrsoftware.org/isinfo.php)
  ```powershell
  winget install --id JRSoftware.InnoSetup -e --scope user
  ```

```powershell
# 실행
dotnet run --project src/CalendarWork.App

# 테스트
dotnet test

# 설치 프로그램 만들기 (테스트 → 게시 → artifacts\installer\CalendarWork-Setup-<버전>.exe)
powershell -ExecutionPolicy Bypass -File build\package.ps1
```

버전과 게시자는 `src/CalendarWork.App/CalendarWork.App.csproj`의 `<Version>`, `<Company>`에서 관리합니다.

개발 중에 실제 데이터를 건드리지 않으려면 환경 변수 `CALENDARWORK_DATA_DIR`에 다른 폴더를 지정하고 실행하세요.

## 프로젝트 구조

```
src/
  CalendarWork.Core      달력·음력·공휴일·반복 일정 계산 (순수 C#)
  CalendarWork.Data      SQLite 저장소, JSON 백업
  CalendarWork.Interop   Win32: 바탕화면 부착, 클릭 투과, 트레이, 전역 단축키
  CalendarWork.App       WPF 앱 (MVVM, CommunityToolkit.Mvvm + DI)
tests/                   xUnit 단위 테스트
installer/               Inno Setup 스크립트
build/                   패키징 스크립트
```

- 기술 스택: .NET 9, WPF, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, System.Text.Json(소스 생성)
- [design.md](design.md): 원래 설계서
- [PROGRESS.md](PROGRESS.md): 현재 구현 상태, 설계와 달라진 점, 코드 규칙, 남은 일
