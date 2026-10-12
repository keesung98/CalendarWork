namespace CalendarWork.App.Services;

/// <summary>색상 테마 적용.</summary>
public interface IThemeService
{
    void Apply(AppTheme theme);
}

/// <summary>Windows 시작 시 자동 실행 등록.</summary>
public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

/// <summary>파일 선택·확인·알림 대화상자. ViewModel은 WPF 대화상자를 직접 띄우지 않는다.</summary>
public interface IDialogService
{
    string? ShowSaveFile(string filter, string defaultFileName);
    string? ShowOpenFile(string filter);
    bool Confirm(string message);
    void ShowInfo(string message);
    void ShowError(string message);
}

/// <summary>창 열기·앱 종료 등 셸 수준 동작.</summary>
public interface IShellService
{
    void ShowSettings();
    void Shutdown();
}

/// <summary>바탕화면 부착 방식이 바뀌었을 때 다시 붙이기.</summary>
public interface IDesktopService
{
    void Reattach();
}
