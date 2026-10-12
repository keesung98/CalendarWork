using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CalendarWork.App.Views;

/// <summary>null이면 Collapsed.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>0이면 Visible (빈 목록 안내 문구용).</summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>true면 Visible, false면 Collapsed.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>메모 색상 번호(0~3) → 현재 테마의 MemoBrush0~3. 테마가 바뀌면 ViewModel이 Refresh해 다시 변환된다.</summary>
public sealed class MemoColorToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Application.Current.TryFindResource($"MemoBrush{(value is int color ? color : 0)}");

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>편집 모드(true)면 크기 조절 손잡이, 잠금 모드면 크기 고정.</summary>
public sealed class EditModeToResizeModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ResizeMode.CanResizeWithGrip : ResizeMode.NoResize;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>bool → 문구 (트레이 메뉴 "편집 모드로 전환"/"잠금 모드로 전환" 등).</summary>
public sealed class BoolToTextConverter : IValueConverter
{
    public string TrueText { get; set; } = "";
    public string FalseText { get; set; } = "";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueText : FalseText;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
