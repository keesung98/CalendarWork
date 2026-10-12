using System.IO;

namespace CalendarWork.App.Services;

/// <summary>
/// 데이터 위치. MSIX 패키징 시 ApplicationData.Current.LocalFolder로 교체 예정이며,
/// 지금은 %LocalAppData%\CalendarWork를 쓴다.
/// 개발·테스트 중에는 환경 변수 CALENDARWORK_DATA_DIR로 실제 데이터와 분리할 수 있다.
/// </summary>
public static class AppPaths
{
    public static string DataDirectory { get; } =
        Environment.GetEnvironmentVariable("CALENDARWORK_DATA_DIR") is { Length: > 0 } custom
            ? custom
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalendarWork");

    public static string Database => Path.Combine(DataDirectory, "calendar.db");
    public static string Settings => Path.Combine(DataDirectory, "settings.json");
    public static string HolidayCache => Path.Combine(DataDirectory, "holidays");
    public static string TrayIcon => Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(HolidayCache);
    }
}
