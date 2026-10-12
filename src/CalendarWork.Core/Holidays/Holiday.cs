using System.Text.Json.Serialization;

namespace CalendarWork.Core.Holidays;

[JsonConverter(typeof(JsonStringEnumConverter<HolidayKind>))]
public enum HolidayKind
{
    [JsonStringEnumMemberName("public")] Public,
    [JsonStringEnumMemberName("substitute")] Substitute,
    [JsonStringEnumMemberName("temporary")] Temporary,
}

public sealed record Holiday(DateOnly Date, string Name, HolidayKind Kind);

/// <summary>연도별 공휴일 파일 (holidays-YYYY.json).</summary>
public sealed class HolidayFile
{
    public int Version { get; set; }
    public int Year { get; set; }
    public List<Holiday> Items { get; set; } = [];
}

/// <summary>원격 manifest.json. 연도별 파일의 버전과 상대 경로.</summary>
public sealed class HolidayManifest
{
    public List<HolidayManifestEntry> Files { get; set; } = [];
}

public sealed class HolidayManifestEntry
{
    public int Year { get; set; }
    public int Version { get; set; }
    public string Path { get; set; } = "";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(HolidayFile))]
[JsonSerializable(typeof(HolidayManifest))]
public sealed partial class HolidayJsonContext : JsonSerializerContext;
