using CalendarWork.Core.Calendar;
using CalendarWork.Core.Holidays;

namespace CalendarWork.Core.Tests;

public class CalendarGridTests
{
    private static HolidayProvider LoadHolidays()
    {
        var provider = new HolidayProvider();
        provider.Load(HolidayProvider.LoadEmbedded());
        return provider;
    }

    [Fact]
    public void October2026_StartsOnSundaySeptember27_AndHas42Cells()
    {
        var grid = new CalendarGrid();
        grid.Build(2026, 10, new DateOnly(2026, 10, 12), HolidayProvider.Empty);

        Assert.Equal(42, grid.Cells.Length);
        Assert.Equal(new DateOnly(2026, 9, 27), grid.FirstDate);
        Assert.Equal(new DateOnly(2026, 11, 7), grid.LastDate);
        Assert.False(grid.Cells[0].IsCurrentMonth);
        Assert.True(grid.Cells[4].IsCurrentMonth); // 10/1 (목)
    }

    [Fact]
    public void MonthStartingOnSunday_HasNoLeadingDays()
    {
        // 2026-02-01은 일요일
        Assert.Equal(new DateOnly(2026, 2, 1), CalendarGrid.GetFirstDate(2026, 2, DayOfWeek.Sunday));
    }

    [Fact]
    public void MondayFirst_ShiftsOffset()
    {
        Assert.Equal(new DateOnly(2026, 9, 28), CalendarGrid.GetFirstDate(2026, 10, DayOfWeek.Monday));
    }

    [Fact]
    public void Build_MarksTodayAndKinds()
    {
        var grid = new CalendarGrid();
        grid.Build(2026, 10, new DateOnly(2026, 10, 12), LoadHolidays());

        var cells = grid.Cells.ToArray();
        Assert.Single(cells, c => c.IsToday);
        Assert.Equal(new DateOnly(2026, 10, 12), cells.Single(c => c.IsToday).Date);

        var foundation = cells.Single(c => c.Date == new DateOnly(2026, 10, 3));
        Assert.Equal(DayKind.Holiday, foundation.Kind);
        Assert.Equal("개천절", foundation.HolidayName);

        Assert.Equal(DayKind.Sunday, cells.Single(c => c.Date == new DateOnly(2026, 10, 4)).Kind);
        Assert.Equal(DayKind.Saturday, cells.Single(c => c.Date == new DateOnly(2026, 10, 10)).Kind);
        Assert.Equal(DayKind.Weekday, cells.Single(c => c.Date == new DateOnly(2026, 10, 12)).Kind);
    }

    [Fact]
    public void Build_ReusesSameMonthRepeatedly()
    {
        var grid = new CalendarGrid();
        for (int m = 1; m <= 12; m++)
        {
            grid.Build(2026, m, new DateOnly(2026, 1, 1), HolidayProvider.Empty);
            Assert.Equal(1, grid.Cells.ToArray().Count(c => c.IsCurrentMonth && c.Date.Day == 1));
        }
    }

    [Fact]
    public void DateKey_RoundTrips()
    {
        var date = new DateOnly(2026, 10, 12);
        Assert.Equal(20261012, DateKey.From(date));
        Assert.Equal(date, DateKey.ToDate(20261012));
    }
}
