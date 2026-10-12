using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarWork.App.ViewModels;

/// <summary>
/// XAML에서 ViewModel을 바인딩하기 위한 진입점. App.xaml에 리소스로 등록되어 있다.
/// <code>DataContext="{Binding Main, Source={StaticResource Locator}}"</code>
/// 인스턴스는 App에서 구성한 DI 컨테이너(Ioc.Default)가 만든다.
/// </summary>
public sealed class ViewModelLocator
{
    /// <summary>달력 창·잠금 해제 버튼이 함께 쓰는 단일 인스턴스.</summary>
    public MainViewModel Main => Ioc.Default.GetRequiredService<MainViewModel>();

    /// <summary>설정 창을 열 때마다 새로 만든다.</summary>
    public SettingsViewModel Settings => Ioc.Default.GetRequiredService<SettingsViewModel>();
}
