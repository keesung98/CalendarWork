using CalendarWork.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CalendarWork.App.ViewModels;

public sealed partial class TodoItemViewModel : ObservableObject
{
    private readonly Action<TodoItemViewModel> _changed;

    public TodoItemViewModel(TodoItem item, Action<TodoItemViewModel> changed)
    {
        Item = item;
        _done = item.Done;
        _changed = changed;
    }

    public TodoItem Item { get; private set; }
    public long Id => Item.Id;
    public string Text => Item.Text;
    public DateOnly? Due => Item.Due;
    public string? DueText => Item.Due is { } d ? $"~{d.Month}/{d.Day}" : null;

    [ObservableProperty] private bool _done;

    // ── 인라인 편집 ──
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editText = "";
    [ObservableProperty] private DateTime? _editDue;

    partial void OnDoneChanged(bool value)
    {
        Item = Item with { Done = value };
        _changed(this);
    }

    public void BeginEdit()
    {
        EditText = Item.Text;
        EditDue = Item.Due?.ToDateTime(TimeOnly.MinValue);
        IsEditing = true;
    }

    /// <summary>편집 내용을 반영한 새 항목. 내용이 비었으면 null.</summary>
    public TodoItem? GetEdited() =>
        string.IsNullOrWhiteSpace(EditText)
            ? null
            : Item with { Text = EditText.Trim(), Due = EditDue is { } d ? DateOnly.FromDateTime(d) : null };

    public void Apply(TodoItem item)
    {
        Item = item;
        IsEditing = false;
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(Due));
        OnPropertyChanged(nameof(DueText));
    }
}
