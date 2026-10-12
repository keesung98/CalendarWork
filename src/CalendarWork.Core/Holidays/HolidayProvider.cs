using System.Collections.Frozen;
using System.Text.Json;

namespace CalendarWork.Core.Holidays;

public interface IHolidayLookup
{
    bool TryGet(DateOnly date, out Holiday holiday);
}

/// <summary>
/// 내장 JSON과 원격에서 받은 JSON을 병합해 <see cref="FrozenDictionary{TKey,TValue}"/>로 O(1) 조회한다.
/// 같은 연도 파일이 여러 개면 version이 가장 높은 것을 쓴다.
/// </summary>
public sealed class HolidayProvider : IHolidayLookup
{
    private const string ResourcePrefix = "CalendarWork.Core.Holidays.";

    private FrozenDictionary<DateOnly, Holiday> _map = FrozenDictionary<DateOnly, Holiday>.Empty;
    private FrozenDictionary<int, int> _versions = FrozenDictionary<int, int>.Empty;

    public static HolidayProvider Empty { get; } = new();

    public bool TryGet(DateOnly date, out Holiday holiday) => _map.TryGetValue(date, out holiday!);

    /// <summary>연도별로 로드된 파일 버전. 원격 갱신 시 비교용.</summary>
    public int GetVersion(int year) => _versions.GetValueOrDefault(year);

    public void Load(IEnumerable<HolidayFile> files)
    {
        var latest = new Dictionary<int, HolidayFile>();
        foreach (var file in files)
        {
            if (!latest.TryGetValue(file.Year, out var existing) || file.Version > existing.Version)
                latest[file.Year] = file;
        }

        var map = new Dictionary<DateOnly, Holiday>();
        foreach (var item in latest.Values.SelectMany(f => f.Items))
        {
            // 같은 날 공휴일이 겹치면 이름을 합친다 (예: 어린이날·부처님오신날)
            map[item.Date] = map.TryGetValue(item.Date, out var prev)
                ? prev with { Name = $"{prev.Name}·{item.Name}" }
                : item;
        }

        _map = map.ToFrozenDictionary();
        _versions = latest.ToFrozenDictionary(p => p.Key, p => p.Value.Version);
    }

    public static HolidayFile Parse(Stream stream) =>
        JsonSerializer.Deserialize(stream, HolidayJsonContext.Default.HolidayFile)
        ?? throw new InvalidDataException("공휴일 파일이 비어 있습니다.");

    /// <summary>어셈블리에 내장된 holidays-YYYY.json 전부.</summary>
    public static IEnumerable<HolidayFile> LoadEmbedded()
    {
        var assembly = typeof(HolidayProvider).Assembly;
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal))
                continue;

            using var stream = assembly.GetManifestResourceStream(name)!;
            yield return Parse(stream);
        }
    }
}
