using System.Collections.Concurrent;
using System.Globalization;

namespace CalendarWork.Core.Calendar;

/// <summary>
/// <see cref="KoreanLunisolarCalendar"/> 래퍼.
/// .NET은 윤달을 월 번호에 포함시켜(윤4월이 있으면 윤4월=5, 5월=6 …) 반환하므로 (월, 윤달 여부)로 보정한다.
/// </summary>
public static class LunarConverter
{
    private static readonly KoreanLunisolarCalendar Calendar = new();
    private static readonly ConcurrentDictionary<int, int> LeapMonthCache = new();

    public static DateOnly MinSolarDate { get; } = DateOnly.FromDateTime(Calendar.MinSupportedDateTime);
    public static DateOnly MaxSolarDate { get; } = DateOnly.FromDateTime(Calendar.MaxSupportedDateTime);

    public static bool IsSupported(DateOnly date) => date >= MinSolarDate && date <= MaxSolarDate;

    /// <summary>양력 → 음력. 범위 밖이면 default(<see cref="LunarDate.IsValid"/> == false).</summary>
    public static LunarDate ToLunar(DateOnly date)
    {
        if (!IsSupported(date))
            return default;

        var dt = date.ToDateTime(TimeOnly.MinValue);
        int year = Calendar.GetYear(dt);
        int monthIndex = Calendar.GetMonth(dt);
        int day = Calendar.GetDayOfMonth(dt);

        int leap = GetLeapMonthIndex(year);
        bool isLeap = false;
        int month = monthIndex;
        if (leap > 0 && monthIndex >= leap)
        {
            isLeap = monthIndex == leap;
            month = monthIndex - 1;
        }
        return new LunarDate(year, month, day, isLeap);
    }

    /// <summary>음력 → 양력. 존재하지 않는 날짜(없는 윤달, 30일이 없는 달 등)면 false.</summary>
    public static bool TryToSolar(int year, int month, int day, bool isLeap, out DateOnly result)
    {
        result = default;
        if (year < Calendar.MinSupportedDateTime.Year || year > Calendar.MaxSupportedDateTime.Year || month is < 1 or > 12)
            return false;

        int leap = GetLeapMonthIndex(year);
        int monthIndex;
        if (isLeap)
        {
            if (leap == 0 || month != leap - 1)
                return false;
            monthIndex = leap;
        }
        else
        {
            monthIndex = leap > 0 && month >= leap ? month + 1 : month;
        }

        if (day < 1 || day > Calendar.GetDaysInMonth(year, monthIndex))
            return false;

        try
        {
            result = DateOnly.FromDateTime(Calendar.ToDateTime(year, monthIndex, day, 0, 0, 0, 0));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>해당 음력 연도의 윤달 (평달 번호 기준, 없으면 0). 예: 윤6월이 있으면 6.</summary>
    public static int GetLeapMonth(int year)
    {
        int index = GetLeapMonthIndex(year);
        return index == 0 ? 0 : index - 1;
    }

    private static int GetLeapMonthIndex(int year) =>
        LeapMonthCache.GetOrAdd(year, static y => Calendar.GetLeapMonth(y));
}
