using System.Globalization;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace CalendarWork.App.Views;

/// <summary>
/// 한글 IME 조합 중인 글자를 확정한 뒤 명령을 실행한다.
/// "시험" 입력 직후 Enter나 (포커스를 받지 않는) 버튼으로 저장하면 마지막 글자 "험"이 아직 조합 중이라
/// 바인딩에는 "시"만 들어가 있다. 포커스를 텍스트 상자에서 빼면 조합이 확정되므로,
/// 그 다음 디스패처 순서에서 명령을 실행한다.
/// </summary>
public sealed class ImeSafeCommand(ICommand inner) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => inner.CanExecuteChanged += value;
        remove => inner.CanExecuteChanged -= value;
    }

    public bool CanExecute(object? parameter) => inner.CanExecute(parameter);

    public void Execute(object? parameter)
    {
        var textBox = Keyboard.FocusedElement as TextBoxBase;
        if (textBox is not null)
            Keyboard.ClearFocus(); // 조합 확정 → Text 갱신 → 바인딩 갱신

        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (inner.CanExecute(parameter))
                inner.Execute(parameter);

            // 연속 입력(할일 추가 등)을 위해 포커스 복원
            if (textBox is { IsVisible: true })
                textBox.Focus();
        });
    }
}

/// <summary>XAML에서 <c>Command="{Binding SaveCommand, Converter={StaticResource ImeSafe}}"</c>로 사용.</summary>
public sealed class ImeSafeCommandConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ICommand command ? new ImeSafeCommand(command) : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
