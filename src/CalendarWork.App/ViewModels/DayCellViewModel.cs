using CalendarWork.Core.Calendar;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalendarWork.App.ViewModels;

/// <summary>칸 안에 보이는 메모/할일 한 줄. Color는 메모 색상 번호(0~3), 실제 색은 View의 MemoColorToBrush 변환기가 정한다.</summary>
public sealed record CellLine(string Text, int Color);

/// <summary>
/// 42개를 한 번만 만들고 월 이동 시 값만 갱신한다 (컨테이너 재생성 방지).
/// </summary>
public sealed partial class DayCellViewModel : ObservableObject
{
    [ObservableProperty] private DateOnly _date;
    [ObservableProperty] private string _dayText = "";
    [ObservableProperty] private string _lunarText = "";
    [ObservableProperty] private string? _holidayName;
    [ObservableProperty] private DayKind _kind;
    [ObservableProperty] private bool _isCurrentMonth;
    [ObservableProperty] private bool _isToday;
    [ObservableProperty] private IReadOnlyList<CellLine> _lines = [];
    [ObservableProperty] private string? _moreText;
    [ObservableProperty] private string? _previewText;

    public void Update(in DayCell cell, string lunarText, IReadOnlyList<CellLine> lines, string? moreText, string? previewText)
    {
        Date = cell.Date;
        DayText = cell.Date.Day.ToString();
        LunarText = lunarText;
        HolidayName = cell.HolidayName;
        Kind = cell.Kind;
        IsCurrentMonth = cell.IsCurrentMonth;
        IsToday = cell.IsToday;
        Lines = lines;
        MoreText = moreText;
        PreviewText = previewText;
    }
}
