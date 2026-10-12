using CalendarWork.Core.Calendar;

namespace CalendarWork.Core.Models;

/// <summary>DB recurrence.rule 값과 같다 (0 weekly, 1 monthly, 2 yearly, 3 lunar-yearly).</summary>
public enum RecurrenceRule
{
    Weekly = 0,
    Monthly = 1,
    Yearly = 2,
    LunarYearly = 3,
}

/// <summary>
/// 반복 일정. <see cref="Anchor"/>(첫 발생일, 양력)부터 규칙대로 반복한다.
/// 없는 날짜 대체 규칙: 매월 31일 → 그 달 말일, 매년 2/29 → 2/28, 음력은 <see cref="LunarRecurrence"/> 규칙.
/// </summary>
public sealed record Recurrence(long Id, string Title, RecurrenceRule Rule, DateOnly Anchor, int Color)
{
    public IEnumerable<DateOnly> Occurrences(DateOnly from, DateOnly to)
    {
        if (to < Anchor)
            yield break;
        if (from < Anchor)
            from = Anchor;

        switch (Rule)
        {
            case RecurrenceRule.Weekly:
                int offset = ((int)Anchor.DayOfWeek - (int)from.DayOfWeek + 7) % 7;
                for (var d = from.AddDays(offset); d <= to; d = d.AddDays(7))
                    yield return d;
                break;

            case RecurrenceRule.Monthly:
                for (var m = new DateOnly(from.Year, from.Month, 1); m <= to; m = m.AddMonths(1))
                {
                    var d = new DateOnly(m.Year, m.Month, Math.Min(Anchor.Day, DateTime.DaysInMonth(m.Year, m.Month)));
                    if (d >= from && d <= to)
                        yield return d;
                }
                break;

            case RecurrenceRule.Yearly:
                for (int y = from.Year; y <= to.Year; y++)
                {
                    var d = new DateOnly(y, Anchor.Month, Math.Min(Anchor.Day, DateTime.DaysInMonth(y, Anchor.Month)));
                    if (d >= from && d <= to)
                        yield return d;
                }
                break;

            case RecurrenceRule.LunarYearly:
                var lunar = LunarConverter.ToLunar(Anchor);
                if (lunar.IsValid)
                {
                    foreach (var d in LunarRecurrence.Occurrences(lunar, from, to))
                        yield return d;
                }
                break;
        }
    }

    /// <summary>"매주 월요일", "매월 15일", "매년 3월 5일", "음력 매년 8월 15일".</summary>
    public string Describe()
    {
        switch (Rule)
        {
            case RecurrenceRule.Weekly:
                return $"매주 {"일월화수목금토"[(int)Anchor.DayOfWeek]}요일";
            case RecurrenceRule.Monthly:
                return $"매월 {Anchor.Day}일";
            case RecurrenceRule.Yearly:
                return $"매년 {Anchor.Month}월 {Anchor.Day}일";
            default:
                var lunar = LunarConverter.ToLunar(Anchor);
                return $"음력 매년 {(lunar.IsLeap ? "윤" : "")}{lunar.Month}월 {lunar.Day}일";
        }
    }
}
