using System.Reflection;

namespace CalendarWork.App.Services;

/// <summary>앱 정보. 버전·만든이는 CalendarWork.App.csproj의 &lt;Version&gt;·&lt;Company&gt;에서 온다.</summary>
public static class AppInfo
{
    /// <summary>"1.0.0" 형식. 빌드 메타데이터(+커밋 해시 등)는 뗀다.</summary>
    public static string Version { get; } =
        (typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
         ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
         ?? "0.0.0").Split('+')[0];

    /// <summary>만든이. csproj의 &lt;Company&gt;(= Authors).</summary>
    public static string Author { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";
}
