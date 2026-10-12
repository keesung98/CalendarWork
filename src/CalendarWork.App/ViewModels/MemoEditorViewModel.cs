using System.Collections.ObjectModel;
using System.Globalization;
using CalendarWork.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalendarWork.App.ViewModels;

public sealed partial class MemoLineViewModel : ObservableObject
{
    [ObservableProperty] private string _text = "";

    [ObservableProperty] private int _color;

    /// <summary>편집기를 열거나 줄을 추가했을 때 이 줄에 커서를 둔다 (View: FocusBehavior).</summary>
    public bool IsFocusRequested { get; init; }

    [RelayCommand]
    private void CycleColor() => Color = (Color + 1) % MemoColors.Count;
}

/// <summary>날짜 칸 더블클릭 시 열리는 메모 편집기. 저장은 닫을 때 한 번만 한다.</summary>
public sealed partial class MemoEditorViewModel : ObservableObject
{
    private readonly Action<MemoEditorViewModel, bool> _close;

    public MemoEditorViewModel(DateOnly date, IEnumerable<Memo> memos, Action<MemoEditorViewModel, bool> close)
    {
        Date = date;
        _close = close;
        Header = date.ToString("M월 d일 (ddd)", CultureInfo.GetCultureInfo("ko-KR"));
        var list = memos.ToList();
        for (int i = 0; i < list.Count; i++)
            Lines.Add(new MemoLineViewModel { Text = list[i].Text, Color = list[i].Color, IsFocusRequested = i == list.Count - 1 });
        if (Lines.Count == 0)
            Lines.Add(new MemoLineViewModel { IsFocusRequested = true });
    }

    public DateOnly Date { get; }
    public string Header { get; }
    public ObservableCollection<MemoLineViewModel> Lines { get; } = [];

    [RelayCommand]
    private void AddLine() => Lines.Add(new MemoLineViewModel { IsFocusRequested = true });

    [RelayCommand]
    private void RemoveLine(MemoLineViewModel line) => Lines.Remove(line);

    [RelayCommand]
    private void Save() => _close(this, true);

    [RelayCommand]
    private void Cancel() => _close(this, false);
}
