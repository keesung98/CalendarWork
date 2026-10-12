using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalendarWork.Interop;

namespace CalendarWork.App.Services;

public enum AppTheme
{
    Dark,
    Light,
}

public enum LunarDisplay
{
    /// <summary>양력 1일과 음력 초하루·보름에만 표시.</summary>
    Partial,
    Daily,
    Off,
}

public sealed class AppSettings
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 820;
    public double Height { get; set; } = 600;

    /// <summary>배경 불투명도 (0.1~1). 글자는 항상 불투명.</summary>
    public double BackgroundOpacity { get; set; } = 0.7;

    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public LunarDisplay LunarDisplay { get; set; } = LunarDisplay.Partial;
    public DesktopLayer AttachMode { get; set; } = DesktopLayer.DesktopOwned;

    /// <summary>마지막 종료 시 잠금 모드였는지. 첫 실행은 위치를 잡을 수 있게 편집 모드.</summary>
    public bool Locked { get; set; }

    public bool TodoPanelVisible { get; set; } = true;

    /// <summary>원격 공휴일 manifest.json의 기본 URL (예: https://user.github.io/calendarwork-holidays/). 비우면 갱신 안 함.</summary>
    public string HolidayUpdateUrl { get; set; } = "";
    public string? HolidayManifestETag { get; set; }
    public DateTimeOffset? HolidayLastChecked { get; set; }
}

// Left/Top은 위치를 아직 정하지 않았으면 NaN이므로 "NaN" 문자열로 저장할 수 있게 한다
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

public sealed class SettingsService
{
    private readonly string _path;

    public SettingsService(string path)
    {
        _path = path;
        Current = Load();
    }

    public AppSettings Current { get; }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                using var stream = File.OpenRead(_path);
                return JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // 손상된 설정은 기본값으로 시작
        }
        return new AppSettings();
    }

    public void Save()
    {
        string temp = _path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, Current, SettingsJsonContext.Default.AppSettings);
        File.Move(temp, _path, overwrite: true);
    }
}
