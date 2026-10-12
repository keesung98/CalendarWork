using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace CalendarWork.App.Views.Behaviors;

/// <summary>
/// 마우스 휠 위/아래를 명령으로 연결.
/// <code>behaviors:MouseWheelBehavior.UpCommand="{Binding PreviousMonthCommand}"</code>
/// </summary>
public static class MouseWheelBehavior
{
    public static readonly DependencyProperty UpCommandProperty = DependencyProperty.RegisterAttached(
        "UpCommand", typeof(ICommand), typeof(MouseWheelBehavior), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty DownCommandProperty = DependencyProperty.RegisterAttached(
        "DownCommand", typeof(ICommand), typeof(MouseWheelBehavior), new PropertyMetadata(null, OnChanged));

    public static ICommand? GetUpCommand(UIElement element) => (ICommand?)element.GetValue(UpCommandProperty);
    public static void SetUpCommand(UIElement element, ICommand? value) => element.SetValue(UpCommandProperty, value);
    public static ICommand? GetDownCommand(UIElement element) => (ICommand?)element.GetValue(DownCommandProperty);
    public static void SetDownCommand(UIElement element, ICommand? value) => element.SetValue(DownCommandProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;
        element.MouseWheel -= OnMouseWheel;
        element.MouseWheel += OnMouseWheel;
    }

    private static void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var element = (UIElement)sender;
        var command = e.Delta > 0 ? GetUpCommand(element) : GetDownCommand(element);
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
            e.Handled = true;
        }
    }
}

/// <summary>
/// 바인딩 값이 true가 되면(또는 true인 채로 화면에 나타나면) 포커스를 준다. 텍스트 상자는 커서를 끝으로.
/// <code>behaviors:FocusBehavior.IsFocused="{Binding IsEditing}"</code>
/// </summary>
public static class FocusBehavior
{
    public static readonly DependencyProperty IsFocusedProperty = DependencyProperty.RegisterAttached(
        "IsFocused", typeof(bool), typeof(FocusBehavior), new PropertyMetadata(false, OnIsFocusedChanged));

    public static bool GetIsFocused(UIElement element) => (bool)element.GetValue(IsFocusedProperty);
    public static void SetIsFocused(UIElement element, bool value) => element.SetValue(IsFocusedProperty, value);

    private static void OnIsFocusedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
            return;
        element.IsVisibleChanged -= OnIsVisibleChanged;
        if (e.NewValue is not true)
            return;

        if (element.IsVisible)
            FocusLater(element);
        else
            element.IsVisibleChanged += OnIsVisibleChanged; // 편집 줄이 나타날 때까지 대기
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var element = (UIElement)sender;
        if (e.NewValue is true && GetIsFocused(element))
        {
            element.IsVisibleChanged -= OnIsVisibleChanged;
            FocusLater(element);
        }
    }

    private static void FocusLater(UIElement element) =>
        element.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            element.Focus();
            if (element is TextBox box)
                box.CaretIndex = box.Text.Length;
        });
}
