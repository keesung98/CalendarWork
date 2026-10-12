using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CalendarWork.App.ViewModels;
using CalendarWork.Interop;

namespace CalendarWork.App.Views.Behaviors;

/// <summary>
/// 코드 비하인드 대신 XAML에서 붙이는 창 동작.
/// <code>
/// behaviors:WindowBehaviors.DragMoveEnabled="{Binding CanDragWindow}"
/// behaviors:WindowBehaviors.CtrlClickCommand="{Binding LockCommand}"
/// behaviors:WindowBehaviors.HideFromAltTab="True"
/// behaviors:WindowBehaviors.CloseOnRequest="True"
/// </code>
/// </summary>
public static class WindowBehaviors
{
    // ── 빈 곳을 누르고 끌면 창 이동 ──────────────────────

    public static readonly DependencyProperty DragMoveEnabledProperty = DependencyProperty.RegisterAttached(
        "DragMoveEnabled", typeof(bool), typeof(WindowBehaviors), new PropertyMetadata(false, OnDragMoveEnabledChanged));

    public static bool GetDragMoveEnabled(Window window) => (bool)window.GetValue(DragMoveEnabledProperty);
    public static void SetDragMoveEnabled(Window window, bool value) => window.SetValue(DragMoveEnabledProperty, value);

    private static void OnDragMoveEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window)
            return;
        // 버튼·입력칸이 처리(Handled)한 클릭은 오지 않도록 일반 이벤트(버블링)에 건다
        window.MouseLeftButtonDown -= OnWindowMouseLeftButtonDown;
        if (e.NewValue is true)
            window.MouseLeftButtonDown += OnWindowMouseLeftButtonDown;
    }

    private static void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 더블클릭(ClickCount 2)은 날짜 칸의 메모 편집으로 넘긴다
        if (!e.Handled && e.ClickCount == 1 && e.ButtonState == MouseButtonState.Pressed)
            ((Window)sender).DragMove();
    }

    // ── Ctrl+클릭 명령 ──────────────────────────────────

    public static readonly DependencyProperty CtrlClickCommandProperty = DependencyProperty.RegisterAttached(
        "CtrlClickCommand", typeof(ICommand), typeof(WindowBehaviors), new PropertyMetadata(null, OnCtrlClickCommandChanged));

    public static ICommand? GetCtrlClickCommand(UIElement element) => (ICommand?)element.GetValue(CtrlClickCommandProperty);
    public static void SetCtrlClickCommand(UIElement element, ICommand? value) => element.SetValue(CtrlClickCommandProperty, value);

    private static void OnCtrlClickCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;
        element.PreviewMouseLeftButtonDown -= OnPreviewCtrlClick;
        if (e.NewValue is not null)
            element.PreviewMouseLeftButtonDown += OnPreviewCtrlClick;
    }

    private static void OnPreviewCtrlClick(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control && !KeyboardState.IsControlDown)
            return;
        var command = GetCtrlClickCommand((UIElement)sender);
        if (command?.CanExecute(null) == true)
        {
            e.Handled = true;
            command.Execute(null);
        }
    }

    // ── Alt+Tab 목록에서 숨김 ───────────────────────────

    public static readonly DependencyProperty HideFromAltTabProperty = DependencyProperty.RegisterAttached(
        "HideFromAltTab", typeof(bool), typeof(WindowBehaviors), new PropertyMetadata(false, OnHideFromAltTabChanged));

    public static bool GetHideFromAltTab(Window window) => (bool)window.GetValue(HideFromAltTabProperty);
    public static void SetHideFromAltTab(Window window, bool value) => window.SetValue(HideFromAltTabProperty, value);

    private static void OnHideFromAltTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window || e.NewValue is not true)
            return;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != 0)
            DesktopAttacher.HideFromAltTab(hwnd);
        else
            window.SourceInitialized += (_, _) => DesktopAttacher.HideFromAltTab(new WindowInteropHelper(window).Handle);
    }

    // ── ViewModel의 닫기 요청(ICloseRequest)으로 창 닫기 ──

    public static readonly DependencyProperty CloseOnRequestProperty = DependencyProperty.RegisterAttached(
        "CloseOnRequest", typeof(bool), typeof(WindowBehaviors), new PropertyMetadata(false, OnCloseOnRequestChanged));

    public static bool GetCloseOnRequest(Window window) => (bool)window.GetValue(CloseOnRequestProperty);
    public static void SetCloseOnRequest(Window window, bool value) => window.SetValue(CloseOnRequestProperty, value);

    private static void OnCloseOnRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window || e.NewValue is not true)
            return;

        void Attach(object? dataContext)
        {
            if (dataContext is ICloseRequest request)
                request.CloseRequested += window.Close;
        }

        Attach(window.DataContext);
        window.DataContextChanged += (_, args) =>
        {
            if (args.OldValue is ICloseRequest old)
                old.CloseRequested -= window.Close;
            Attach(args.NewValue);
        };
        window.Closed += (_, _) =>
        {
            if (window.DataContext is ICloseRequest request)
                request.CloseRequested -= window.Close;
        };
    }
}
