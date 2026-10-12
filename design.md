# 바탕화면 달력·할일 위젯 설계서

작성일: 2026-10-12 · 가칭: **바탕달력 (DeskCal)**
목표: 1~2개월 내 MVP 출시, MS Store 무료 배포 + Pro 평생 라이선스 + 후원.

---

## 1. 제품 개요

### 1.1 한 줄 정의
Windows 바탕화면에 붙어 있는 투명 달력. 한국 공휴일·대체공휴일·음력·D-day를 보여 주고, 날짜 칸에서 바로 메모와 할일을 관리한다.

### 1.2 대상 사용자
- 사무직, 학생, 개인사업자 등 PC 앞에서 일정을 자주 확인하는 사람
- 현재 DesktopCal(중국산, 광고·한국 공휴일 부정확), 포스트잇, 스티커 메모를 쓰는 사람

### 1.3 차별점
| 항목 | 바탕달력 | DesktopCal | Windows 11 위젯 | 스티커 메모 |
|---|---|---|---|---|
| 바탕화면 상주 | O | O | X (패널) | 창 형태 |
| 한국 공휴일·대체공휴일 정확도 | 원격 갱신 | 부정확한 경우 있음 | 계정 의존 | X |
| 음력 표시 | O | 일부 | X | X |
| 광고 | 없음 | 있음 | - | - |
| 리소스 | 유휴 CPU 0%, 메모리 60MB 이하 목표 | 무거운 편 | - | - |

---

## 2. 기능 범위

### 2.1 MVP (무료)
1. 월간 달력 바탕화면 표시 (6주 × 7일 그리드)
2. 한국 공휴일·대체공휴일·임시공휴일 표시 (원격 데이터 갱신)
3. 음력 날짜 표시 (매월 1일·15일 또는 매일, 설정)
4. 날짜 칸 더블클릭 → 인라인 메모 입력 (여러 줄, 색상 4종)
5. 할일 목록 패널 (체크, 마감일 지정 시 달력에 표시)
6. D-day 1개
7. 잠금 모드(클릭 투과) / 편집 모드 전환 (트레이 메뉴 + 전역 단축키)
8. 위치·크기·투명도 조절, 라이트/다크 테마
9. Windows 시작 시 자동 실행
10. 데이터 내보내기/가져오기 (JSON)

### 2.2 Pro (평생 9,900원)
1. D-day 무제한
2. 다중 모니터 (모니터별 달력)
3. 테마 팩 (폰트·색상·배경 프리셋), 사용자 정의 색상
4. 반복 일정 (매주/매월/매년, 음력 매년 — 제사·생일용)
5. 구글 캘린더 읽기 전용 연동
6. 주간 보기 / 2개월 보기

### 2.3 범위 밖 (v2 이후)
- 클라우드 동기화, 모바일 앱, 알림 팝업, 공유 캘린더

---

## 3. 기술 스택

| 영역 | 선택 | 이유 |
|---|---|---|
| 런타임 | .NET 10 (LTS) + WPF | 장기 지원, 최신 JIT(Dynamic PGO) |
| MVVM | CommunityToolkit.Mvvm | 소스 생성기 기반, 리플렉션 없음 |
| Win32 Interop | `LibraryImport` 소스 생성 (또는 CsWin32) | 마샬링 스텁 컴파일 타임 생성, 시작 비용 감소 |
| 저장소 | SQLite (Microsoft.Data.Sqlite, WAL) | 단일 파일, 날짜 범위 인덱스 조회 |
| 음력 | `System.Globalization.KoreanLunisolarCalendar` | .NET 내장, 918~2050년 지원, 외부 데이터 불필요 |
| 공휴일 | 앱 내장 JSON + 원격 JSON 갱신 | 대체·임시공휴일 대응 |
| 직렬화 | System.Text.Json 소스 생성 (`JsonSerializerContext`) | 리플렉션 제거, 빠른 시작 |
| 패키징 | MSIX (Store) | Store 배포, 자동 업데이트, StartupTask |
| 결제 | Windows.Services.Store (Durable add-on) | Store 내 결제 처리 |
| 배포 최적화 | ReadyToRun, TieredPGO | 콜드 스타트 단축 (WPF는 Trimming 미지원) |

---

## 4. 솔루션 구조

```
DeskCal.sln
├─ src/
│  ├─ DeskCal.Core/        // 순수 C# (net10.0): 달력 계산, 공휴일 병합, 음력, 도메인 모델
│  ├─ DeskCal.Data/        // SQLite 저장소, 마이그레이션, 리포지토리
│  ├─ DeskCal.Interop/     // Win32: WorkerW 부착, 클릭 투과, 전역 단축키, 모니터 열거
│  ├─ DeskCal.App/         // WPF: View, ViewModel, 트레이, 설정
│  └─ DeskCal.Package/     // MSIX 패키징 프로젝트
└─ tests/
   ├─ DeskCal.Core.Tests/  // 공휴일·음력·그리드 계산 단위 테스트
   └─ DeskCal.Data.Tests/
```

의존 방향: `App → Core, Data, Interop` / `Data → Core` / `Core`는 아무것도 참조하지 않음.
Core를 UI·OS와 분리해 계산 로직을 전부 단위 테스트로 고정한다.

---

## 5. 핵심 설계

### 5.1 바탕화면 부착 (가장 중요한 부분)

#### 방식 A: WorkerW 부착 (기본)
아이콘 레이어 뒤, 배경화면 위에 창을 붙인다. Win+D(바탕화면 보기)를 눌러도 사라지지 않는다.

1. `FindWindow("Progman")` → `SendMessageTimeout(progman, 0x052C, 0xD, 0x1)` 로 WorkerW 생성 유도
2. `EnumWindows`로 `SHELLDLL_DefView`를 자식으로 가진 창을 찾고, 그 **다음 WorkerW**를 대상으로 삼는다
3. **Windows 11 24H2 이후**: WorkerW가 Progman의 자식으로 생성되는 구조로 바뀌었으므로, Progman 자식 WorkerW도 탐색한다 (두 경로 모두 구현, 버전 분기 없이 탐색 순서로 처리)
4. `SetParent(hwnd, workerW)` 후 `WS_CHILD` 스타일 적용, 좌표는 WorkerW 기준으로 변환

#### 방식 B: 최하단 창 (폴백)
WorkerW 탐색 실패 시 사용. 일반 최상위 창을 `HWND_BOTTOM`에 두고 `WM_WINDOWPOSCHANGING`을 가로채 Z-order를 항상 최하단으로 유지한다. Win+D에서는 숨겨지는 한계가 있다.

#### 탐색기 재시작 대응
- `RegisterWindowMessage("TaskbarCreated")` 를 수신하면 1초 지연 후 재부착
- 부착 상태를 `IsWindow(parent)`로 확인하는 헬스체크는 **폴링하지 않고** 위 메시지와 `WM_DISPLAYCHANGE`에서만 수행

#### 클릭 투과 (잠금 모드)
- 잠금: `WS_EX_TRANSPARENT | WS_EX_LAYERED` 추가 → 마우스가 바탕화면 아이콘으로 통과
- 편집: `WS_EX_TRANSPARENT` 제거
- 전환: 트레이 메뉴, 전역 단축키(`RegisterHotKey`, 기본 Ctrl+Alt+D), 달력 위에서 Ctrl 누른 채 클릭

### 5.2 투명 렌더링과 성능

WPF의 `AllowsTransparency=True`는 레이어드 윈도우(UpdateLayeredWindow)로 동작해서 **다시 그릴 때마다 전체 비트맵을 복사**한다. 그래서 핵심 원칙은 "자주 다시 그리지 않는 것"이다.

- 애니메이션 없음 (호버 효과도 편집 모드에서만)
- `DropShadowEffect`, `BlurEffect` 사용 금지 → 그림자가 필요하면 반투명 Border로 대체
- 모든 Brush·Pen은 생성 시 `Freeze()`, 테마 리소스도 Frozen
- 날짜 칸은 `ItemsControl` + `UniformGrid`(42칸). 템플릿은 TextBlock 2~3개 수준으로 얕게 유지
- 메모 표시는 칸당 최대 3줄 + "외 N건", 텍스트 측정 비용 제한
- `TextOptions.TextFormattingMode="Display"` (작은 글씨 선명도)
- 월 이동 시 ViewModel의 42개 셀 객체를 **재사용**하고 값만 갱신 (컬렉션 재생성 금지 → 컨테이너 재생성 방지)
- 4K·다중 모니터에서 프레임 비용 측정 후, 필요하면 셀 그리드를 `DrawingVisual` 기반 커스텀 컨트롤로 교체 (2단계 최적화 옵션)

목표 지표:
| 지표 | 목표 |
|---|---|
| 유휴 CPU | 0% (타이머 폴링 없음) |
| 작업 집합 메모리 | 60MB 이하 |
| 콜드 스타트 → 표시 | 1초 이하 (ReadyToRun) |
| 월 이동 반응 | 16ms 이하 |

### 5.3 시간 이벤트 (폴링 없음)
주기 타이머 대신 이벤트로만 갱신한다.
- **자정 갱신**: 다음 자정까지 남은 시간으로 `DispatcherTimer` 1회 예약 → 실행 후 다음 자정으로 재예약
- `SystemEvents.TimeChanged` (사용자가 시계 변경), `SystemEvents.PowerModeChanged` (절전 복귀), `SystemEvents.SessionSwitch` (잠금 해제) 수신 시 "오늘" 재계산
- 표준 시간대 변경은 `TimeZoneInfo.ClearCachedData()` 후 재계산

### 5.4 달력 계산 (DeskCal.Core)

```csharp
public readonly record struct DayCell(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    DayKind Kind,          // Weekday, Saturday, Sunday, Holiday
    string? HolidayName,
    LunarDate Lunar);

public readonly record struct LunarDate(int Year, int Month, int Day, bool IsLeap);
```

- 월 그리드: 해당 월 1일의 요일로 시작 오프셋 계산, 항상 42칸 고정 (레이아웃 흔들림 방지)
- `DayCell`은 값 타입. 그리드는 `DayCell[42]` 배열 하나를 재사용
- 음력 변환: `KoreanLunisolarCalendar`는 윤달을 월 번호에 포함시켜 반환하므로 `GetLeapMonth(year)`로 보정해 (월, 윤달 여부)로 변환. 연도별 윤달 값은 `Dictionary<int,int>`로 캐시
- 음력 반복 일정은 매년 음력→양력 역변환. 윤달·30일 없는 달은 "평달/29일로 대체" 규칙을 설정으로 제공

### 5.5 공휴일 데이터

#### 형식 (`holidays-2026.json`)
```json
{
  "version": 3,
  "year": 2026,
  "items": [
    { "date": "2026-01-01", "name": "신정", "kind": "public" },
    { "date": "2026-03-02", "name": "대체공휴일(삼일절)", "kind": "substitute" }
  ]
}
```

#### 갱신 흐름
1. 앱에 당해·다음 해 JSON 내장 (오프라인 첫 실행 대응)
2. 원격: 정적 호스팅(GitHub Pages 또는 CDN)에 `manifest.json` + 연도별 파일
3. 앱은 하루 1회 이하, 시작 후 지연 실행으로 `manifest.json`을 **ETag 조건부 요청** → 변경된 연도만 다운로드
4. 원본 데이터는 개발자가 공공데이터포털 「한국천문연구원 특일 정보」 API로 생성해 업로드 (사용자 PC는 API 키 불필요)
5. 임시공휴일 발표 시 manifest 버전만 올리면 전 사용자 반영

조회는 `FrozenDictionary<DateOnly, Holiday>` 로 로드 후 O(1).

### 5.6 데이터 저장 (DeskCal.Data)

위치: MSIX면 `ApplicationData.Current.LocalFolder`, 아니면 `%LocalAppData%\DeskCal`.

```sql
CREATE TABLE memo (
  id        INTEGER PRIMARY KEY,
  date      INTEGER NOT NULL,      -- yyyymmdd 정수 (범위 조회 최적화)
  text      TEXT    NOT NULL,
  color     INTEGER NOT NULL DEFAULT 0,
  sort      INTEGER NOT NULL DEFAULT 0,
  updated   INTEGER NOT NULL
);
CREATE INDEX ix_memo_date ON memo(date);

CREATE TABLE todo (
  id        INTEGER PRIMARY KEY,
  text      TEXT    NOT NULL,
  due       INTEGER,               -- yyyymmdd, NULL 허용
  done      INTEGER NOT NULL DEFAULT 0,
  sort      INTEGER NOT NULL DEFAULT 0,
  updated   INTEGER NOT NULL
);
CREATE INDEX ix_todo_due ON todo(due) WHERE due IS NOT NULL;

CREATE TABLE dday (
  id INTEGER PRIMARY KEY, title TEXT NOT NULL, target INTEGER NOT NULL, lunar INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE recurrence (           -- Pro
  id INTEGER PRIMARY KEY, title TEXT NOT NULL,
  rule INTEGER NOT NULL,            -- 0 weekly,1 monthly,2 yearly,3 lunar-yearly
  anchor INTEGER NOT NULL, color INTEGER NOT NULL DEFAULT 0
);

PRAGMA user_version = 1;            -- 마이그레이션 버전
```

- 월 표시 시 `WHERE date BETWEEN ? AND ?` 한 번으로 42칸 메모 조회 (그리드 시작~끝)
- 연결은 앱 수명 동안 1개 유지, 명령은 `Prepare` 후 재사용
- 쓰기는 메모 편집 종료 시점에만 (키 입력마다 저장 금지), WAL 모드
- 설정(위치, 테마, 투명도)은 별도 `settings.json` (소스 생성 직렬화)

### 5.7 다중 모니터·DPI
- 앱 매니페스트: `PerMonitorV2` DPI 인식
- `EnumDisplayMonitors`로 모니터 목록, 모니터별 위치를 **모니터 장치 ID 기준**으로 저장 (해상도·배치 변경에도 복원)
- `WM_DISPLAYCHANGE` 수신 시 화면 밖으로 나간 창은 주 모니터로 이동
- 무료: 주 모니터 1개 / Pro: 모니터별 독립 달력

### 5.8 트레이·단축키
- 트레이 아이콘: `System.Windows.Forms.NotifyIcon` 대신 Shell_NotifyIcon 직접 호출 또는 경량 라이브러리 (WinForms 어셈블리 로드로 메모리 약 10MB 증가 방지)
- 메뉴: 편집/잠금 전환, 오늘로 이동, 설정, 할일 패널, 후원하기, 종료
- 전역 단축키: 잠금 전환, 할일 패널 열기 (설정에서 변경)

### 5.9 라이선스 (Pro)
- Store Durable add-on 1개 (`deskcal.pro`)
- 시작 시 `StoreContext.GetAppLicenseAsync()` 결과를 캐시해 오프라인에서도 Pro 유지 (캐시 유효 30일, 이후 재검증)
- Pro 기능은 `ILicenseService.IsPro` 하나로 분기, UI는 잠금 아이콘 + 구매 유도
- 후원: 트레이 메뉴 "후원하기" → 외부 후원 페이지 링크 (토스 후원, Buy Me a Coffee 등)

### 5.10 자동 실행
- MSIX `windows.startupTask` 확장 사용 (사용자가 작업 관리자에서 끌 수 있음)
- 시작 시 부착·렌더링을 먼저 하고, 공휴일 갱신 확인·라이선스 확인은 `Task.Delay(5s)` 후 백그라운드로 실행

---

## 6. 화면 구성

```
┌──────────────────────────────────────────┐
│  2026년 10월            ◀  오늘  ▶   ⚙  │  ← 편집 모드에서만 버튼 표시
│  D-25 기말고사                            │
├──────┬──────┬──────┬──────┬──────┬──────┬──────┤
│ 일   │ 월   │ 화   │ 수   │ 목   │ 금   │ 토   │
├──────┼──────┼──────┼──────┼──────┼──────┼──────┤
│ 27   │ 28   │ 29   │ 30   │ 1    │ 2    │ 3    │
│      │      │      │      │8.21  │      │개천절│
│      │      │      │      │      │      │      │
├──────┼──────┼──────┼──────┼──────┼──────┼──────┤
│ ...  날짜 / 음력(작게) / 공휴일명 / 메모 최대 3줄  │
└──────────────────────────────────────────┘
          [할일 패널: 달력 오른쪽에 접이식]
```

- 일요일·공휴일 빨강, 토요일 파랑, 오늘은 테두리 강조
- 이전/다음 달 날짜는 흐리게
- 편집 모드: 테두리 점선 + 드래그 이동·리사이즈 핸들 표시

---

## 7. 개발 일정 (5주 + 출시 1주)

| 주차 | 작업 | 완료 기준 |
|---|---|---|
| 1 | 솔루션 구성, Core(그리드·음력·공휴일) + 단위 테스트 | 2020~2030년 공휴일·음력 테스트 통과 |
| 2 | Interop: WorkerW 부착, 폴백, 탐색기 재시작, 클릭 투과 | Win10 22H2 / Win11 23H2 / 24H2 실기 확인 |
| 3 | 달력 View, 메모 인라인 편집, SQLite 저장 | 메모 CRUD, 월 이동 16ms 이하 |
| 4 | 할일 패널, D-day, 트레이, 단축키, 설정, 테마 | MVP 기능 완성 |
| 5 | Pro 기능(다중 모니터, 반복, 테마 팩), Store 라이선스 | 구매→해제 흐름 확인 |
| 6 | 성능 측정·튜닝, MSIX 패키징, Store 제출 | 목표 지표 달성, 심사 제출 |

구글 캘린더 연동은 출시 후 첫 업데이트로 미룬다 (OAuth 검수 기간 때문).

---

## 8. 테스트 계획
- **단위**: 공휴일 병합, 대체공휴일, 음력 변환(윤달 포함), 42칸 그리드, 음력 반복 일정 역변환
- **통합**: SQLite 마이그레이션, 대량 메모(1만 건) 월 조회 시간
- **수동 실기 매트릭스**: Win10 / Win11 23H2 / Win11 24H2 이상 × 단일·다중 모니터 × 100%/150%/200% DPI
- **충돌 확인**: Wallpaper Engine, Lively Wallpaper(둘 다 WorkerW 사용), 가상 데스크톱 전환, 탐색기 강제 종료
- **성능**: PerfView/dotnet-counters로 유휴 CPU·GC 횟수, 시작 시간 측정

---

## 9. 리스크와 대응

| 리스크 | 영향 | 대응 |
|---|---|---|
| Windows 업데이트로 WorkerW 구조 변경 | 부착 실패 | 탐색 경로 2종 + 최하단 창 폴백, 실패 시 자동 전환 |
| Wallpaper Engine 등과 WorkerW 충돌 | 달력이 가려짐 | 감지 시 폴백 모드 안내 |
| 레이어드 윈도우 4K 렌더링 비용 | 편집 시 버벅임 | 다시 그리기 최소화, 필요 시 DrawingVisual 전환 |
| 임시공휴일 지연 반영 | 신뢰도 하락 | 원격 manifest로 당일 반영 |
| KoreanLunisolarCalendar 범위(2050년까지) | 장기 D-day 제한 | 2050년 이후 입력 시 안내 |
| 무료 사용자 대비 낮은 유료 전환 | 수익 미미 | Pro 가치는 다중 모니터·음력 반복(제사·생일)에 집중, 후원 병행 |

---

## 10. 출시·수익화
- **MS Store**: 무료 앱 + Pro add-on 9,900원. 2025년 9월부터 개인 개발자 등록비 없음
- **스토어 문구 키워드**: 바탕화면 달력, 데스크톱 캘린더, 공휴일, 음력, 할일, D-day
- **유입**: 네이버 블로그·IT 커뮤니티(클리앙, 뽐뿌 소프트웨어 게시판)에 "광고 없는 바탕화면 달력" 소개
- **지표**: 설치 수, 7일 유지율, Pro 전환율 (익명 사용 통계는 옵트인으로만)

---

## 11. 결정이 필요한 항목
1. 앱 이름 (가칭 바탕달력)
2. Pro 가격: 평생 9,900원 vs 연 4,900원
3. 원격 공휴일 데이터 호스팅 위치 (GitHub Pages 권장, 무료)
4. 할일 패널을 달력 옆 고정으로 할지, 별도 위젯 창으로 분리할지
