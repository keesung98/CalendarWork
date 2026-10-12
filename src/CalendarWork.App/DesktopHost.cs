using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using CalendarWork.App.Services;
using CalendarWork.App.ViewModels;
using CalendarWork.App.Views;
using CalendarWork.Interop;

namespace CalendarWork.App;

/// <summary>
/// 바탕화면 달력 창의 수명과 바탕화면 부착을 관리하는 View 쪽 서비스.
/// 모드의 기준은 <see cref="MainViewModel.IsEditMode"/> 하나이며, 이 클래스는 그 값이 바뀔 때 창에 반영만 한다.
/// <list type="bullet">
/// <item>잠금 모드: 바탕화면에 부착 + 클릭 투과, 오른쪽 위에 잠금 해제 버튼.</item>
/// <item>편집 모드: 일반 창으로 떼어 내 이동·리사이즈·편집.</item>
/// </list>
/// 트레이 아이콘·전역 단축키·탐색기 재시작 대응도 여기서 처리한다.
/// </summary>
public sealed class DesktopHost : IDesktopService, IDisposable
{
    private const int HotKeyToggleLock = 1;
    private const int HotKeyToggleTodo = 2;
    private const uint VK_D = 0x44;
    private const uint VK_T = 0x54;
    private static readonly TimeSpan ReattachDelay = TimeSpan.FromSeconds(1);

    private readonly SettingsService _settingsService;
    private readonly AppSettings _settings;
    private readonly MainViewModel _viewModel;
    private readonly ShellMessageWindow _shell = new();

    private MainWindow _window = null!;
    private LockBadgeWindow _badge = null!;
    private DesktopAttacher _attacher = null!;
    private bool _attached;
    private bool _disposing;

    public DesktopHost(SettingsService settingsService, MainViewModel viewModel)
    {
        _settingsService = settingsService;
        _settings = settingsService.Current;
        _viewModel = viewModel;
    }

    public void Start()
    {
        CreateWindow();

        _shell.ShowTrayIcon(AppPaths.TrayIcon, "CalendarWork");
        _shell.TrayMouse += OnTrayMouse;
        _shell.HotKeyPressed += OnHotKey;
        _shell.ExplorerRestarted += () => AfterDelay(ReattachDelay, Reattach);
        _shell.DisplayChanged += () => AfterDelay(ReattachDelay, OnDisplayChanged);
        _shell.RegisterHotKey(HotKeyToggleLock, HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_D);
        _shell.RegisterHotKey(HotKeyToggleTodo, HotKeyModifiers.Control | HotKeyModifiers.Alt, VK_T);

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyMode(activate: false);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsEditMode))
            ApplyMode(activate: true);
    }

    // ── 창 ────────────────────────────────────────────

    private void CreateWindow()
    {
        _window = new MainWindow(); // DataContext는 XAML에서 연결된다
        ApplyBounds(_window);

        _window.SourceInitialized += (_, _) => _attacher = new DesktopAttacher(new WindowInteropHelper(_window).Handle);
        _window.LocationChanged += (_, _) => RememberBounds();
        _window.SizeChanged += (_, _) => RememberBounds();
        _window.Closed += OnWindowClosed;
        _window.Show();
        RememberBounds(); // 처음 정한 위치도 바로 기록 (창을 옮기지 않아도 저장되도록)

        // 소유 창이므로 달력 창이 파괴되면 함께 파괴된다
        _badge = new LockBadgeWindow { Owner = _window };
        _attached = false;
    }

    /// <summary>탐색기가 죽으면 소유자(Progman)와 함께 달력 창도 파괴될 수 있다. 그 경우 다시 만든다.</summary>
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_disposing)
            return;
        AfterDelay(ReattachDelay, () =>
        {
            CreateWindow();
            ApplyMode(activate: false);
        });
    }

    private void ApplyBounds(Window window)
    {
        var work = SystemParameters.WorkArea;
        window.Width = Math.Max(window.MinWidth, _settings.Width);
        window.Height = Math.Max(window.MinHeight, _settings.Height);

        bool valid = !double.IsNaN(_settings.Left) && !double.IsNaN(_settings.Top) && IsOnScreen(_settings.Left, _settings.Top, window.Width);
        window.Left = valid ? _settings.Left : work.Right - window.Width - 40;
        window.Top = valid ? _settings.Top : work.Top + 40;
    }

    private static bool IsOnScreen(double left, double top, double width)
    {
        // 제목 영역 일부라도 가상 화면 안에 있으면 OK
        double vl = SystemParameters.VirtualScreenLeft, vt = SystemParameters.VirtualScreenTop;
        double vr = vl + SystemParameters.VirtualScreenWidth, vb = vt + SystemParameters.VirtualScreenHeight;
        return left + width > vl + 40 && left < vr - 40 && top >= vt - 10 && top < vb - 40;
    }

    /// <summary>편집 모드(최상위 창)일 때만 WPF 좌표가 화면 좌표이므로 그때만 저장한다.</summary>
    private void RememberBounds()
    {
        if (_attacher is null || _attacher.State != DesktopAttachState.Detached || _window.WindowState != WindowState.Normal)
            return;
        _settings.Left = _window.Left;
        _settings.Top = _window.Top;
        _settings.Width = _window.Width;
        _settings.Height = _window.Height;
    }

    // ── 모드 반영 ─────────────────────────────────────

    private void ApplyMode(bool activate)
    {
        if (_viewModel.IsEditMode)
        {
            Detach();
            _shell.SetTrayTooltip("CalendarWork · 편집 중 (Ctrl+Alt+D로 잠금)");
            if (activate)
                _window.Activate();
        }
        else
        {
            RememberBounds();
            var state = Attach();
            _shell.SetTrayTooltip(state == DesktopAttachState.BottomMost ? "CalendarWork · 잠금 (호환 모드)" : "CalendarWork · 잠금");
        }

        _settings.Locked = !_viewModel.IsEditMode;
        SaveSettings();
    }

    private DesktopAttachState Attach()
    {
        var state = _attacher.Attach(_settings.AttachMode);
        _attached = true;

        // 달력은 클릭 투과이므로 바탕화면에서 풀 수 있게 잠금 해제 버튼을 띄운다
        _badge.Left = _settings.Left + _settings.Width - _badge.Width - 16;
        _badge.Top = _settings.Top + 12;
        _badge.Show();
        return state;
    }

    private void Detach()
    {
        _badge.Hide();
        if (_attached)
            _attacher.Detach();
        _attached = false;
    }

    /// <summary>부착 방식 변경·탐색기 재시작 시 다시 붙인다 (잠금 모드일 때만).</summary>
    public void Reattach()
    {
        if (!_viewModel.IsEditMode && _window.IsLoaded)
        {
            Detach();
            Attach();
        }
    }

    private void OnDisplayChanged()
    {
        bool locked = !_viewModel.IsEditMode;
        if (locked)
            Detach();
        if (!IsOnScreen(_window.Left, _window.Top, _window.Width))
        {
            _settings.Left = double.NaN;
            ApplyBounds(_window);
            RememberBounds();
        }
        if (locked)
            Attach();
    }

    // ── 트레이·단축키 ─────────────────────────────────

    private void OnTrayMouse(TrayMouseAction action)
    {
        switch (action)
        {
            case TrayMouseAction.LeftClick:
                _viewModel.ToggleLockCommand.Execute(null);
                break;
            case TrayMouseAction.RightClick:
                // 메뉴 바깥 클릭 시 닫히도록 먼저 전경으로
                ShellMessageWindow.BringToForeground(_shell.Handle);
                if (Application.Current.TryFindResource("TrayMenu") is ContextMenu menu)
                    menu.IsOpen = true;
                break;
        }
    }

    private void OnHotKey(int id)
    {
        switch (id)
        {
            case HotKeyToggleLock:
                _viewModel.ToggleLockCommand.Execute(null);
                break;
            case HotKeyToggleTodo:
                _viewModel.ToggleTodoPanelCommand.Execute(null);
                break;
        }
    }

    // ── 공통 ──────────────────────────────────────────

    public void SaveSettings()
    {
        try
        {
            _settingsService.Save();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // 다음 저장 시점에 재시도
        }
    }

    private static void AfterDelay(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
    }

    public void Dispose()
    {
        _disposing = true;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        RememberBounds();
        SaveSettings();
        _shell.Dispose();
        _window.Close();
    }
}
