using CalendarWork.Core.Holidays;

namespace CalendarWork.Core.Calendar;

/// <summary>
/// 6주 × 7일 = 42칸 고정 월 그리드. 레이아웃이 흔들리지 않도록 항상 42칸을 채우고,
/// 월 이동 시 내부 배열을 재사용한다.
/// </summary>
public sealed class CalendarGrid
{
    public const int Columns = 7;
    public const int Rows = 6;
    public const int CellCount = Columns * Rows;

    private readonly DayCell[] _cells = new DayCell[CellCount];

    public CalendarGrid(DayOfWeek firstDayOfWeek = DayOfWeek.Sunday)
    {
        FirstDayOfWeek = firstDayOfWeek;
    }

    public DayOfWeek FirstDayOfWeek { get; }
    public int Year { get; private set; }
    public int Month { get; private set; }

    /// <summary>그리드 첫 칸 날짜 (이전 달일 수 있음).</summary>
    public DateOnly FirstDate { get; private set; }

    /// <summary>그리드 마지막 칸 날짜 (다음 달일 수 있음).</summary>
    public DateOnly LastDate => FirstDate.AddDays(CellCount - 1);

    public ReadOnlySpan<DayCell> Cells => _cells;

    public static DateOnly GetFirstDate(int year, int month, DayOfWeek firstDayOfWeek)
    {
        var first = new DateOnly(year, month, 1);
        int offset = ((int)first.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return first.AddDays(-offset);
    }

    public void Build(int year, int month, DateOnly today, IHolidayLookup holidays)
    {
        Year = year;
        Month = month;
        FirstDate = GetFirstDate(year, month, FirstDayOfWeek);

        var date = FirstDate;
        for (int i = 0; i < CellCount; i++, date = date.AddDays(1))
        {
            string? holidayName = holidays.TryGet(date, out var holiday) ? holiday.Name : null;
            var kind = holidayName is not null
                ? DayKind.Holiday
                : date.DayOfWeek switch
                {
                    DayOfWeek.Sunday => DayKind.Sunday,
                    DayOfWeek.Saturday => DayKind.Saturday,
                    _ => DayKind.Weekday,
                };

            _cells[i] = new DayCell(
                date,
                IsCurrentMonth: date.Month == month,
                IsToday: date == today,
                kind,
                holidayName,
                LunarConverter.ToLunar(date));
        }
    }
}
