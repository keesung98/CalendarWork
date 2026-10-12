namespace CalendarWork.Core.Calendar;

/// <summary>DB 저장용 yyyymmdd 정수 변환. 범위 조회(BETWEEN)를 정수 비교로 처리한다.</summary>
public static class DateKey
{
    public static int From(DateOnly date) => date.Year * 10000 + date.Month * 100 + date.Day;

    public static DateOnly ToDate(int key) => new(key / 10000, key / 100 % 100, key % 100);
}
