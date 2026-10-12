namespace CalendarWork.Core.Calendar;

/// <summary>
/// 음력 매년 반복(제사·생일·음력 D-day)의 양력 날짜 계산.
/// 해당 해에 없는 날짜는 설계서 규칙대로 대체한다:
/// 윤달 → 그 해에 같은 윤달이 없으면 평달, 30일 → 그 달이 29일까지면 29일.
/// </summary>
public static class LunarRecurrence
{
    /// <summary>음력 <paramref name="lunarYear"/>년의 (월, 일) 양력 날짜. 변환 범위 밖이면 null.</summary>
    public static DateOnly? ToSolar(int lunarYear, int month, int day, bool isLeap)
    {
        if (isLeap && LunarConverter.TryToSolar(lunarYear, month, day, isLeap: true, out var leapDate))
            return leapDate;
        if (isLeap && day == 30 && LunarConverter.TryToSolar(lunarYear, month, 29, isLeap: true, out leapDate))
            return leapDate;

        if (LunarConverter.TryToSolar(lunarYear, month, day, isLeap: false, out var date))
            return date;
        if (day == 30 && LunarConverter.TryToSolar(lunarYear, month, 29, isLeap: false, out date))
            return date;
        return null;
    }

    /// <summary><paramref name="from"/> 당일 또는 이후 가장 가까운 발생일. 범위(2050년) 밖이면 null.</summary>
    public static DateOnly? NextOccurrence(LunarDate anchor, DateOnly from)
    {
        // 양력 1~2월은 음력으로 전년도일 수 있으므로 한 해 앞부터 본다
        for (int year = from.Year - 1; year <= from.Year + 1; year++)
        {
            if (ToSolar(year, anchor.Month, anchor.Day, anchor.IsLeap) is { } date && date >= from)
                return date;
        }
        return null;
    }

    /// <summary>[from, to] 구간의 모든 발생일.</summary>
    public static IEnumerable<DateOnly> Occurrences(LunarDate anchor, DateOnly from, DateOnly to)
    {
        for (int year = from.Year - 1; year <= to.Year; year++)
        {
            if (ToSolar(year, anchor.Month, anchor.Day, anchor.IsLeap) is { } date && date >= from && date <= to)
                yield return date;
        }
    }
}
