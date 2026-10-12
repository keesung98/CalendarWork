using System.IO;
using System.Windows;
using System.Windows.Threading;
using CalendarWork.App.Services;
using CalendarWork.App.ViewModels;
using CalendarWork.Core.Holidays;
using CalendarWork.Data;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarWork.App;

/// <summary>
/// 앱 시작점이자 컴포지션 루트. 서비스·ViewModel을 DI 컨테이너(Ioc.Default)에 등록하면,
/// 각 창은 XAML의 ViewModelLocator를 통해 자기 ViewModel을 받는다.
/// </summary>
public partial class App : Application
{
    private static readonly TimeSpan BackgroundWorkDelay = TimeSpan.FromSeconds(5);

    private Mutex? _singleInstance;
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\CalendarWork.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        AppPaths.EnsureCreated();
        _services = ConfigureServices();
        Ioc.Default.ConfigureServices(_services);

        var settings = _services.GetRequiredService<SettingsService>();
        _services.GetRequiredService<IThemeService>().Apply(settings.Current.Theme);
        _services.GetRequiredService<HolidayService>().Load();

        // 부착·렌더링을 먼저 하고, 네트워크 작업은 지연 실행
        _services.GetRequiredService<DesktopHost>().Start();
        _ = UpdateHolidaysLaterAsync();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 설정·데이터
        services.AddSingleton(_ => new SettingsService(AppPaths.Settings));
        services.AddSingleton(sp => sp.GetRequiredService<SettingsService>().Current);
        services.AddSingleton(_ => new CalendarDatabase(AppPaths.Database));
        services.AddSingleton<MemoRepository>();
        services.AddSingleton<TodoRepository>();
        services.AddSingleton<DDayRepository>();
        services.AddSingleton<RecurrenceRepository>();
        services.AddSingleton<BackupService>();
        services.AddSingleton(sp => new HolidayService(sp.GetRequiredService<AppSettings>(), AppPaths.HolidayCache));
        services.AddSingleton<IHolidayLookup>(sp => sp.GetRequiredService<HolidayService>().Provider);
        services.AddSingleton<ClockService>();

        // View 쪽 서비스
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IStartupService, StartupRegistration>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<DesktopHost>();
        services.AddSingleton<IDesktopService>(sp => sp.GetRequiredService<DesktopHost>());

        // ViewModel
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private async Task UpdateHolidaysLaterAsync()
    {
        await Task.Delay(BackgroundWorkDelay);
        if (_services is null)
            return;

        var holidays = _services.GetRequiredService<HolidayService>();
        if (await holidays.CheckForUpdatesAsync())
        {
            holidays.Load();
            _services.GetRequiredService<MainViewModel>().Refresh();
        }
        _services.GetRequiredService<DesktopHost>().SaveSettings();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            File.AppendAllText(Path.Combine(AppPaths.DataDirectory, "error.log"),
                $"[{DateTimeOffset.Now:O}] {e.Exception}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        MessageBox.Show($"오류가 발생했습니다.\n{e.Exception.Message}", "CalendarWork", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // DesktopHost·ClockService·CalendarDatabase 등 IDisposable 싱글턴을 함께 정리한다
        _services?.Dispose();
        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }
        base.OnExit(e);
    }
}
