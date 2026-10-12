using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using CalendarWork.Core.Holidays;

namespace CalendarWork.App.Services;

/// <summary>
/// 내장 공휴일 + 로컬 캐시(원격에서 받은 파일)를 병합해 로드하고,
/// 하루 1회 이하로 원격 manifest.json을 ETag 조건부 요청해 바뀐 연도만 받는다.
/// </summary>
public sealed class HolidayService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    private readonly AppSettings _settings;
    private readonly string _cacheDir;

    public HolidayService(AppSettings settings, string cacheDir)
    {
        _settings = settings;
        _cacheDir = cacheDir;
    }

    public HolidayProvider Provider { get; } = new();

    public void Load()
    {
        var files = new List<HolidayFile>(HolidayProvider.LoadEmbedded());
        foreach (var path in Directory.EnumerateFiles(_cacheDir, "holidays-*.json"))
        {
            try
            {
                using var stream = File.OpenRead(path);
                files.Add(HolidayProvider.Parse(stream));
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            {
                File.Delete(path); // 손상된 캐시는 버리고 내장 데이터 사용
            }
        }
        Provider.Load(files);
    }

    /// <summary>갱신된 파일이 있으면 true. 실패는 조용히 무시한다 (다음 실행 때 재시도).</summary>
    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.HolidayUpdateUrl))
            return false;
        if (_settings.HolidayLastChecked is { } last && DateTimeOffset.Now - last < CheckInterval)
            return false;

        try
        {
            var baseUri = new Uri(_settings.HolidayUpdateUrl.TrimEnd('/') + "/");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, "manifest.json"));
            if (_settings.HolidayManifestETag is { } etag)
                request.Headers.IfNoneMatch.ParseAdd(etag);

            using var response = await http.SendAsync(request, cancellationToken);
            _settings.HolidayLastChecked = DateTimeOffset.Now;
            if (response.StatusCode == HttpStatusCode.NotModified)
                return false;
            response.EnsureSuccessStatusCode();

            await using var manifestStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var manifest = await JsonSerializer.DeserializeAsync(manifestStream, HolidayJsonContext.Default.HolidayManifest, cancellationToken);
            if (manifest is null)
                return false;

            bool updated = false;
            foreach (var entry in manifest.Files.Where(f => f.Version > Provider.GetVersion(f.Year)))
            {
                var bytes = await http.GetByteArrayAsync(new Uri(baseUri, entry.Path), cancellationToken);
                using (var check = new MemoryStream(bytes))
                {
                    var file = HolidayProvider.Parse(check);
                    if (file.Year != entry.Year)
                        continue;
                }
                await File.WriteAllBytesAsync(Path.Combine(_cacheDir, $"holidays-{entry.Year}.json"), bytes, cancellationToken);
                updated = true;
            }

            _settings.HolidayManifestETag = response.Headers.ETag?.ToString();
            return updated;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or IOException or UriFormatException)
        {
            return false;
        }
    }
}
