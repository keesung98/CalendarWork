using Microsoft.Win32;

namespace CalendarWork.App.Services;

/// <summary>
/// Windows 시작 시 자동 실행 (HKCU Run 키). MSIX 패키징 후에는 windows.startupTask 확장으로 교체한다.
/// </summary>
public sealed class StartupRegistration : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CalendarWork";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(ValueName, $"\"{exe}\"");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
