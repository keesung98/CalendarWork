using CalendarWork.Core.Calendar;

namespace CalendarWork.Core.Models;

/// <summary>메모 색상 4종 (0: 기본, 1: 빨강, 2: 초록, 3: 파랑).</summary>
public static class MemoColors
{
    public const int Count = 4;
}

public sealed record Memo(long Id, DateOnly Date, string Text, int Color, int Sort);

public sealed record TodoItem(long Id, string Text, DateOnly? Due, bool Done, int Sort);

/// <summary>
/// D-day. <see cref="Lunar"/>가 false면 <see cref="Target"/> 하루짜리,
/// true면 <see cref="Target"/>의 음력 월·일로 매년 반복(생일·제사)한다.
/// </summary>
public sealed record DDay(long Id, string Title, DateOnly Target, bool Lunar)
{
    public LunarDate LunarAnchor => LunarConverter.ToLunar(Target);

    /// <summary>오늘 기준 대상 날짜. 음력이면 다음 발생일.</summary>
    public DateOnly GetTargetDate(DateOnly today) =>
        Lunar && LunarAnchor.IsValid ? LunarRecurrence.NextOccurrence(LunarAnchor, today) ?? Target : Target;

    /// <summary>"D-25 기말고사", "D-Day 제사 (음 3.5)", "D+3 여행" 형식.</summary>
    public string Format(DateOnly today)
    {
        int diff = GetTargetDate(today).DayNumber - today.DayNumber;
        string d = diff switch
        {
            > 0 => $"D-{diff}",
            0 => "D-Day",
            _ => $"D+{-diff}",
        };
        return Lunar && LunarAnchor.IsValid ? $"{d} {Title} (음 {LunarAnchor})" : $"{d} {Title}";
    }
}
