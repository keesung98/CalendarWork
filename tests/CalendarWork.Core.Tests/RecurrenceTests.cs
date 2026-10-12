using CalendarWork.Core.Calendar;
using CalendarWork.Core.Models;

namespace CalendarWork.Core.Tests;

public class RecurrenceTests
{
    [Fact]
    public void LunarNextOccurrence_Chuseok()
    {
        var chuseok = new LunarDate(2026, 8, 15, false);
        Assert.Equal(new DateOnly(2026, 9, 25), LunarRecurrence.NextOccurrence(chuseok, new DateOnly(2026, 1, 1)));
        Assert.Equal(new DateOnly(2026, 9, 25), LunarRecurrence.NextOccurrence(chuseok, new DateOnly(2026, 9, 25)));
        Assert.Equal(new DateOnly(2027, 9, 15), LunarRecurrence.NextOccurrence(chuseok, new DateOnly(2026, 9, 26)));
    }

    [Fact]
    public void LunarNextOccurrence_EarlySolarYearUsesPreviousLunarYear()
    {
        // 음력 2025년 12월 15일 = 양력 2026-02-02
        Assert.True(LunarConverter.TryToSolar(2025, 12, 15, false, out var expected));
        var anchor = new LunarDate(2020, 12, 15, false);
        Assert.Equal(expected, LunarRecurrence.NextOccurrence(anchor, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void LunarToSolar_LeapFallsBackToNormalMonth()
    {
        // 2025년 윤6월 1일 → 윤6월이 없는 2026년에는 평6월 1일
        Assert.True(LunarConverter.TryToSolar(2026, 6, 1, false, out var normal));
        Assert.Equal(normal, LunarRecurrence.ToSolar(2026, 6, 1, isLeap: true));
    }

    [Fact]
    public void LunarToSolar_Day30FallsBackTo29()
    {
        // 30일이 없는 달을 찾아 29일로 대체되는지 확인
        for (int month = 1; month <= 12; month++)
        {
            if (!LunarConverter.TryToSolar(2026, month, 30, false, out _))
            {
                Assert.True(LunarConverter.TryToSolar(2026, month, 29, false, out var day29));
                Assert.Equal(day29, LunarRecurrence.ToSolar(2026, month, 30, false));
                return;
            }
        }
        Assert.Fail("2026년에 29일까지인 음력 달이 없음");
    }

    [Fact]
    public void LunarDDay_FormatsNextOccurrence()
    {
        // 기준일 2025-10-06 (음력 8/15, 추석)로 등록 → 2026-10-12 기준 다음 추석은 2027-09-15
        var dday = new DDay(1, "추석", new DateOnly(2025, 10, 6), Lunar: true);
        var today = new DateOnly(2026, 10, 12);
        int expected = new DateOnly(2027, 9, 15).DayNumber - today.DayNumber;
        Assert.Equal($"D-{expected} 추석 (음 8.15)", dday.Format(today));
    }

    [Fact]
    public void SolarDDay_IsOneTime()
    {
        var dday = new DDay(1, "시험", new DateOnly(2026, 10, 10), Lunar: false);
        Assert.Equal("D+2 시험", dday.Format(new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void Weekly_SameWeekdayFromAnchor()
    {
        var r = new Recurrence(1, "회의", RecurrenceRule.Weekly, new DateOnly(2026, 10, 7), 0); // 수요일
        var dates = r.Occurrences(new DateOnly(2026, 9, 27), new DateOnly(2026, 11, 7)).ToList();
        Assert.Equal([new(2026, 10, 7), new(2026, 10, 14), new(2026, 10, 21), new(2026, 10, 28), new(2026, 11, 4)], dates);
        Assert.Equal("매주 수요일", r.Describe());
    }

    [Fact]
    public void Monthly_ClampsToLastDay()
    {
        var r = new Recurrence(1, "정산", RecurrenceRule.Monthly, new DateOnly(2026, 1, 31), 0);
        var dates = r.Occurrences(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 30)).ToList();
        Assert.Equal([new(2026, 1, 31), new(2026, 2, 28), new(2026, 3, 31), new(2026, 4, 30)], dates);
    }

    [Fact]
    public void Yearly_Feb29FallsBackTo28()
    {
        var r = new Recurrence(1, "생일", RecurrenceRule.Yearly, new DateOnly(2024, 2, 29), 0);
        Assert.Equal([new DateOnly(2026, 2, 28)], r.Occurrences(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        Assert.Equal([new DateOnly(2028, 2, 29)], r.Occurrences(new DateOnly(2028, 1, 1), new DateOnly(2028, 12, 31)));
    }

    [Fact]
    public void LunarYearly_FollowsLunarDate()
    {
        var r = new Recurrence(1, "추석", RecurrenceRule.LunarYearly, new DateOnly(2025, 10, 6), 0);
        Assert.Equal([new DateOnly(2026, 9, 25)], r.Occurrences(new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31)));
        Assert.Equal("음력 매년 8월 15일", r.Describe());
    }

    [Fact]
    public void Occurrences_NeverBeforeAnchor()
    {
        var r = new Recurrence(1, "운동", RecurrenceRule.Weekly, new DateOnly(2026, 10, 20), 0);
        Assert.All(r.Occurrences(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), d => Assert.True(d >= r.Anchor));
    }
}
