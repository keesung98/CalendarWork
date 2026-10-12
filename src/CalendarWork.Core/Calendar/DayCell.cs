namespace CalendarWork.Core.Calendar;

public enum DayKind
{
    Weekday,
    Saturday,
    Sunday,
    Holiday,
}

/// <summary>달력 그리드의 한 칸. 값 타입으로 두고 <see cref="CalendarGrid"/>가 배열 하나를 재사용한다.</summary>
public readonly record struct DayCell(
    DateOnly Date,
    bool IsCurrentMonth,
    bool IsToday,
    DayKind Kind,
    string? HolidayName,
    LunarDate Lunar);

/// <summary>음력 날짜. <see cref="IsValid"/>가 false면 변환 범위(918~2050년) 밖이다.</summary>
public readonly record struct LunarDate(int Year, int Month, int Day, bool IsLeap)
{
    public bool IsValid => Year != 0;

    public override string ToString() => IsValid ? $"{(IsLeap ? "윤" : "")}{Month}.{Day}" : "";
}
