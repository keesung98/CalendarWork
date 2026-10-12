using CalendarWork.Core.Calendar;

namespace CalendarWork.Core.Tests;

public class LunarConverterTests
{
    [Theory]
    [InlineData(2026, 2, 17, 2026, 1, 1, false)]   // 2026 설날
    [InlineData(2026, 9, 25, 2026, 8, 15, false)]  // 2026 추석
    [InlineData(2026, 10, 1, 2026, 8, 21, false)]
    [InlineData(2025, 7, 25, 2025, 6, 1, true)]    // 2025 윤6월 1일
    [InlineData(2025, 6, 25, 2025, 6, 1, false)]   // 2025 평6월 1일
    [InlineData(2025, 9, 6, 2025, 7, 15, false)]   // 윤달 이후 월 보정
    public void ToLunar_KnownDates(int y, int m, int d, int ly, int lm, int ld, bool leap)
    {
        Assert.Equal(new LunarDate(ly, lm, ld, leap), LunarConverter.ToLunar(new DateOnly(y, m, d)));
    }

    [Fact]
    public void GetLeapMonth_2025IsSixth()
    {
        Assert.Equal(6, LunarConverter.GetLeapMonth(2025));
        Assert.Equal(0, LunarConverter.GetLeapMonth(2026));
    }

    [Fact]
    public void TryToSolar_RejectsNonexistentLeapMonth()
    {
        Assert.False(LunarConverter.TryToSolar(2026, 6, 1, isLeap: true, out _));
        Assert.True(LunarConverter.TryToSolar(2025, 6, 1, isLeap: true, out var date));
        Assert.Equal(new DateOnly(2025, 7, 25), date);
    }

    [Fact]
    public void RoundTrip_2020To2030()
    {
        for (var date = new DateOnly(2020, 1, 1); date <= new DateOnly(2030, 12, 31); date = date.AddDays(1))
        {
            var lunar = LunarConverter.ToLunar(date);
            Assert.True(lunar.IsValid);
            Assert.True(LunarConverter.TryToSolar(lunar.Year, lunar.Month, lunar.Day, lunar.IsLeap, out var back));
            Assert.Equal(date, back);
        }
    }

    [Fact]
    public void OutOfRange_ReturnsInvalid()
    {
        Assert.False(LunarConverter.ToLunar(new DateOnly(2060, 1, 1)).IsValid);
    }

    [Fact]
    public void Format_PrefixesLeap()
    {
        Assert.Equal("윤6.1", new LunarDate(2025, 6, 1, true).ToString());
        Assert.Equal("8.15", new LunarDate(2026, 8, 15, false).ToString());
    }
}
