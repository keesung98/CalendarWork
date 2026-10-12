using CalendarWork.Core.Calendar;
using CalendarWork.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalendarWork.App.ViewModels;

public enum DDayEditorResult
{
    Cancel,
    Save,
    Delete,
}

public sealed partial class DDayEditorViewModel : ObservableObject
{
    private readonly Action<DDayEditorViewModel, DDayEditorResult> _close;

    public DDayEditorViewModel(DDay? existing, DateOnly today, Action<DDayEditorViewModel, DDayEditorResult> close)
    {
        _close = close;
        Id = existing?.Id ?? 0;
        _title = existing?.Title ?? "";
        _target = (existing?.Target ?? today.AddDays(30)).ToDateTime(TimeOnly.MinValue);
        _isLunar = existing?.Lunar ?? false;
    }

    public long Id { get; }
    public bool CanDelete => Id != 0;

    [ObservableProperty] private string _title;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LunarText))]
    private DateTime? _target;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LunarText))]
    private bool _isLunar;

    /// <summary>음력 선택 시 "음력 3월 5일 · 매년 반복" 안내.</summary>
    public string? LunarText
    {
        get
        {
            if (!IsLunar || Target is not { } target)
                return null;
            var lunar = LunarConverter.ToLunar(DateOnly.FromDateTime(target));
            return lunar.IsValid
                ? $"음력 {(lunar.IsLeap ? "윤" : "")}{lunar.Month}월 {lunar.Day}일 · 매년 반복"
                : "음력 변환 범위(1918~2050년) 밖의 날짜입니다";
        }
    }

    public DDay ToModel() => new(Id, Title.Trim(), DateOnly.FromDateTime(Target!.Value), IsLunar);

    // CanExecute로 버튼을 막지 않는다: IME 조합 중에는 Title이 아직 비어 있을 수 있음
    [RelayCommand]
    private void Save()
    {
        if (!string.IsNullOrWhiteSpace(Title) && Target is not null)
            _close(this, DDayEditorResult.Save);
    }

    [RelayCommand]
    private void Delete() => _close(this, DDayEditorResult.Delete);

    [RelayCommand]
    private void Cancel() => _close(this, DDayEditorResult.Cancel);
}
