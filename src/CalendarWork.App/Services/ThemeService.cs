using System.Windows;

namespace CalendarWork.App.Services;

/// <summary>Themes/Light.xaml·Dark.xaml 교체. 모든 브러시는 XAML에서 Freeze된 상태로 로드된다.</summary>
public sealed class ThemeService : IThemeService
{
    public void Apply(AppTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var next = new ResourceDictionary { Source = new Uri($"pack://application:,,,/Themes/{theme}.xaml", UriKind.Absolute) };

        // 0번 = 색상 테마, 1번 = 컨트롤 스타일
        if (dictionaries.Count > 0)
            dictionaries[0] = next;
        else
            dictionaries.Add(next);
    }
}
