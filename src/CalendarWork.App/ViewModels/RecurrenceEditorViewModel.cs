using System.Collections.ObjectModel;
using CalendarWork.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalendarWork.App.ViewModels;

public sealed record RecurrenceItemViewModel(Recurrence Model)
{
    public string Title => Model.Title;
    public string Description => Model.Describe();
    public int Color => Model.Color;
}

/// <summary>반복 일정 목록 + 새 반복 일정 추가 (매주/매월/매년/음력 매년).</summary>
public sealed partial class RecurrenceEditorViewModel : ObservableObject
{
    private readonly Func<Recurrence, Recurrence> _save;
    private readonly Action<Recurrence> _delete;
    private readonly Action<RecurrenceEditorViewModel> _close;

    public RecurrenceEditorViewModel(
        IEnumerable<Recurrence> existing,
        DateOnly anchor,
        Func<Recurrence, Recurrence> save,
        Action<Recurrence> delete,
        Action<RecurrenceEditorViewModel> close)
    {
        _save = save;
        _delete = delete;
        _close = close;
        foreach (var r in existing)
            Items.Add(new RecurrenceItemViewModel(r));
        _newAnchor = anchor.ToDateTime(TimeOnly.MinValue);
    }

    public ObservableCollection<RecurrenceItemViewModel> Items { get; } = [];
    public IReadOnlyList<string> RuleOptions { get; } = ["매주", "매월", "매년", "음력 매년"];

    [ObservableProperty] private string _newTitle = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewDescription))]
    private int _newRuleIndex = (int)RecurrenceRule.Yearly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NewDescription))]
    private DateTime? _newAnchor;

    [ObservableProperty] private int _newColor;

    /// <summary>입력 중인 규칙 미리보기 ("음력 매년 8월 15일").</summary>
    public string NewDescription => NewAnchor is { } d
        ? new Recurrence(0, "", (RecurrenceRule)NewRuleIndex, DateOnly.FromDateTime(d), 0).Describe() + "부터"
        : "";

    [RelayCommand]
    private void CycleColor() => NewColor = (NewColor + 1) % MemoColors.Count;

    [RelayCommand]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(NewTitle) || NewAnchor is not { } anchor)
            return;
        var saved = _save(new Recurrence(0, NewTitle, (RecurrenceRule)NewRuleIndex, DateOnly.FromDateTime(anchor), NewColor));
        Items.Add(new RecurrenceItemViewModel(saved));
        NewTitle = "";
    }

    [RelayCommand]
    private void Delete(RecurrenceItemViewModel? item)
    {
        if (item is null)
            return;
        _delete(item.Model);
        Items.Remove(item);
    }

    [RelayCommand]
    private void Close() => _close(this);
}
