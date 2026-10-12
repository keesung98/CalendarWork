using System.IO;
using System.Text.Json;
using CalendarWork.App.Services;
using CalendarWork.Data;
using CalendarWork.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Data.Sqlite;

namespace CalendarWork.App.ViewModels;

/// <summary>설정 창. 값이 바뀌면 즉시 적용하고 바로 저장한다.</summary>
public sealed partial class SettingsViewModel : ObservableObject, ICloseRequest
{
    private const string JsonFilter = "JSON 파일 (*.json)|*.json";

    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly MainViewModel _main;
    private readonly IThemeService _theme;
    private readonly IStartupService _startup;
    private readonly IDialogService _dialogs;
    private readonly IDesktopService _desktop;
    private readonly BackupService _backup;

    public SettingsViewModel(
        SettingsService settingsService,
        MainViewModel main,
        IThemeService theme,
        IStartupService startup,
        IDialogService dialogs,
        IDesktopService desktop,
        BackupService backup)
    {
        _settingsService = settingsService;
        _settings = settingsService.Current;
        _main = main;
        _theme = theme;
        _startup = startup;
        _dialogs = dialogs;
        _desktop = desktop;
        _backup = backup;

        _backgroundOpacity = _settings.BackgroundOpacity;
        _isDarkTheme = _settings.Theme == AppTheme.Dark;
        _lunarDisplayIndex = (int)_settings.LunarDisplay;
        _attachModeIndex = (int)_settings.AttachMode;
        _holidayUpdateUrl = _settings.HolidayUpdateUrl;
        _startWithWindows = startup.IsEnabled;
    }

    public event Action? CloseRequested;

    public IReadOnlyList<string> LunarDisplayOptions { get; } = ["1일·초하루·보름만", "매일", "표시 안 함"];
    public IReadOnlyList<string> AttachModeOptions { get; } = ["바탕화면 고정 (권장)", "아이콘 뒤 (WorkerW · 구버전 Windows)", "최하단 창 (호환 모드)"];
    public string HotKeysText { get; } = "Ctrl+Alt+D  잠금/편집 전환\nCtrl+Alt+T  할일 패널 열기/닫기\n편집 모드에서 Ctrl+클릭  잠금";
    public string DataFolderText { get; } = $"데이터 위치: {AppPaths.DataDirectory}";
    public string VersionText { get; } = $"버전 {AppInfo.Version}";
    public string AuthorText { get; } = $"만든이: {AppInfo.Author}";

    [ObservableProperty] private double _backgroundOpacity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightTheme))]
    private bool _isDarkTheme;

    /// <summary>라디오 버튼용. 다크의 반대.</summary>
    public bool IsLightTheme
    {
        get => !IsDarkTheme;
        set => IsDarkTheme = !value;
    }

    [ObservableProperty] private int _lunarDisplayIndex;
    [ObservableProperty] private int _attachModeIndex;
    [ObservableProperty] private string _holidayUpdateUrl;
    [ObservableProperty] private bool _startWithWindows;

    partial void OnBackgroundOpacityChanged(double value)
    {
        _main.BackgroundOpacity = Math.Clamp(value, 0.1, 1.0);
        Save();
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        _settings.Theme = value ? AppTheme.Dark : AppTheme.Light;
        _theme.Apply(_settings.Theme);
        _main.Refresh(); // 메모 색상 다시 그리기
        Save();
    }

    partial void OnLunarDisplayIndexChanged(int value)
    {
        _settings.LunarDisplay = (LunarDisplay)value;
        _main.Refresh();
        Save();
    }

    partial void OnAttachModeIndexChanged(int value)
    {
        _settings.AttachMode = (DesktopLayer)value;
        _desktop.Reattach();
        Save();
    }

    partial void OnHolidayUpdateUrlChanged(string value)
    {
        _settings.HolidayUpdateUrl = value.Trim();
        Save();
    }

    partial void OnStartWithWindowsChanged(bool value) => _startup.SetEnabled(value);

    [RelayCommand]
    private void Export()
    {
        if (_dialogs.ShowSaveFile(JsonFilter, $"CalendarWork-{DateTime.Now:yyyyMMdd}.json") is not { } path)
            return;
        try
        {
            using var stream = File.Create(path);
            _backup.Export(stream);
            _dialogs.ShowInfo("내보냈습니다.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError($"내보내기 실패: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Import()
    {
        if (_dialogs.ShowOpenFile(JsonFilter) is not { } path)
            return;
        if (!_dialogs.Confirm("현재 메모·할일·D-day·반복 일정을 모두 지우고 파일 내용으로 바꿉니다. 계속할까요?"))
            return;
        try
        {
            using var stream = File.OpenRead(path);
            _backup.Import(stream);
            _main.ReloadAll();
            _dialogs.ShowInfo("가져왔습니다.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or SqliteException)
        {
            _dialogs.ShowError($"가져오기 실패: {ex.Message}");
        }
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private void Save()
    {
        try
        {
            _settingsService.Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 다음 변경 때 다시 저장된다
        }
    }
}
