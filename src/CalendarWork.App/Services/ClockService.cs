using System.Windows.Threading;
using Microsoft.Win32;

namespace CalendarWork.App.Services;

/// <summary>
/// "오늘"이 바뀌는 순간만 알린다. 주기 타이머 없이 다음 자정에 1회 예약하고,
/// 시계 변경·절전 복귀·잠금 해제·표준 시간대 변경 시 재계산한다.
/// </summary>
public sealed class ClockService : IDisposable
{
    private readonly DispatcherTimer _midnightTimer;
    private DateOnly _today;

    public ClockService()
    {
        _today = DateOnly.FromDateTime(DateTime.Now);
        _midnightTimer = new DispatcherTimer(DispatcherPriority.Background);
        _midnightTimer.Tick += (_, _) => Recheck();

        SystemEvents.TimeChanged += OnSystemEvent;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        ScheduleNextMidnight();
    }

    public DateOnly Today => _today;

    /// <summary>날짜가 바뀌었을 때 (UI 스레드).</summary>
    public event Action<DateOnly>? DayChanged;

    private void OnSystemEvent(object? sender, EventArgs e) => Dispatch();

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            Dispatch();
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
            Dispatch();
    }

    // SystemEvents는 별도 스레드에서 올라올 수 있으므로 UI 스레드로 넘긴다
    private void Dispatch() => _midnightTimer.Dispatcher.BeginInvoke(() =>
    {
        TimeZoneInfo.ClearCachedData();
        Recheck();
    });

    private void Recheck()
    {
        var now = DateOnly.FromDateTime(DateTime.Now);
        if (now != _today)
        {
            _today = now;
            DayChanged?.Invoke(now);
        }
        ScheduleNextMidnight();
    }

    private void ScheduleNextMidnight()
    {
        _midnightTimer.Stop();
        var now = DateTime.Now;
        // 자정 직후 1초 여유
        _midnightTimer.Interval = now.Date.AddDays(1).AddSeconds(1) - now;
        _midnightTimer.Start();
    }

    public void Dispose()
    {
        _midnightTimer.Stop();
        SystemEvents.TimeChanged -= OnSystemEvent;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }
}
