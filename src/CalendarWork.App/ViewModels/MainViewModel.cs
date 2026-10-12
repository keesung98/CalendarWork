using System.Collections.ObjectModel;
using CalendarWork.App.Services;
using CalendarWork.Core.Calendar;
using CalendarWork.Core.Holidays;
using CalendarWork.Core.Models;
using CalendarWork.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CalendarWork.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int MaxCellLines = 3;

    private readonly AppSettings _settings;
    private readonly IHolidayLookup _holidays;
    private readonly MemoRepository _memos;
    private readonly TodoRepository _todos;
    private readonly DDayRepository _ddays;
    private readonly RecurrenceRepository _recurrenceRepo;
    private readonly ClockService _clock;
    private readonly IShellService _shell;
    private readonly CalendarGrid _grid = new();
    private List<Recurrence> _recurrences;
    private DDay? _dday;
    private int _year;
    private int _month;

    public MainViewModel(
        AppSettings settings,
        IHolidayLookup holidays,
        MemoRepository memos,
        TodoRepository todos,
        DDayRepository ddays,
        RecurrenceRepository recurrences,
        ClockService clock,
        IShellService shell)
    {
        _settings = settings;
        _holidays = holidays;
        _memos = memos;
        _todos = todos;
        _ddays = ddays;
        _recurrenceRepo = recurrences;
        _recurrences = recurrences.GetAll();
        _clock = clock;
        _shell = shell;

        for (int i = 0; i < Cells.Length; i++)
            Cells[i] = new DayCellViewModel();

        _isEditMode = !settings.Locked;
        _isTodoPanelVisible = settings.TodoPanelVisible;
        _newTodoDue = clock.Today.ToDateTime(TimeOnly.MinValue);
        _backgroundOpacity = settings.BackgroundOpacity;

        var today = clock.Today;
        _year = today.Year;
        _month = today.Month;
        clock.DayChanged += OnDayChanged;

        ReloadTodos();
        _dday = _ddays.GetAll().FirstOrDefault(); // 무료: D-day 1개
        Refresh();
    }

    public DayCellViewModel[] Cells { get; } = new DayCellViewModel[CalendarGrid.CellCount];
    public ObservableCollection<TodoItemViewModel> Todos { get; } = [];
    public IReadOnlyList<string> WeekdayNames { get; } = ["일", "월", "화", "수", "목", "금", "토"];

    [ObservableProperty] private string _title = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAddDDay))]
    private string? _dDayText;

    /// <summary>편집 모드(true) / 잠금 모드(false). 바탕화면 부착은 DesktopHost가 이 값을 보고 처리한다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAddDDay), nameof(CanDragWindow))]
    [NotifyCanExecuteChangedFor(nameof(LockCommand), nameof(UnlockCommand))]
    private bool _isEditMode = true;

    public bool ShowAddDDay => IsEditMode && DDayText is null;

    /// <summary>편집 모드이고 편집기가 열려 있지 않을 때만 창을 끌어 옮길 수 있다.</summary>
    public bool CanDragWindow => IsEditMode && MemoEditor is null && DDayEditor is null && RecurrenceEditor is null;
    [ObservableProperty] private bool _isTodoPanelVisible;
    [ObservableProperty] private double _backgroundOpacity;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDragWindow))]
    [NotifyCanExecuteChangedFor(nameof(LockCommand))]
    private MemoEditorViewModel? _memoEditor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDragWindow))]
    [NotifyCanExecuteChangedFor(nameof(LockCommand))]
    private DDayEditorViewModel? _dDayEditor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDragWindow))]
    [NotifyCanExecuteChangedFor(nameof(LockCommand))]
    private RecurrenceEditorViewModel? _recurrenceEditor;

    [ObservableProperty] private string _newTodoText = "";

    /// <summary>새 할일 마감일. 기본값은 오늘 (지우면 마감 없음).</summary>
    [ObservableProperty] private DateTime? _newTodoDue;

    partial void OnIsTodoPanelVisibleChanged(bool value) => _settings.TodoPanelVisible = value;

    partial void OnBackgroundOpacityChanged(double value) => _settings.BackgroundOpacity = value;

    private void OnDayChanged(DateOnly today)
    {
        // 기본값(어제 날짜)을 그대로 두고 있었다면 오늘로 갱신
        if (NewTodoDue == today.AddDays(-1).ToDateTime(TimeOnly.MinValue))
            NewTodoDue = today.ToDateTime(TimeOnly.MinValue);

        // 어제가 속한 달을 보고 있었다면 오늘의 달로 따라간다
        var yesterday = today.AddDays(-1);
        if (_year == yesterday.Year && _month == yesterday.Month)
        {
            _year = today.Year;
            _month = today.Month;
        }
        Refresh();
    }

    /// <summary>현재 월 그리드를 다시 계산하고 42칸 값만 갱신한다.</summary>
    public void Refresh()
    {
        var today = _clock.Today;
        _grid.Build(_year, _month, today, _holidays);

        var memosByDate = _memos.GetRange(_grid.FirstDate, _grid.LastDate).ToLookup(m => m.Date);
        var todosByDate = Todos
            .Where(t => !t.Done && t.Due is { } d && d >= _grid.FirstDate && d <= _grid.LastDate)
            .ToLookup(t => t.Due!.Value);
        var recurrencesByDate = _recurrences
            .SelectMany(r => r.Occurrences(_grid.FirstDate, _grid.LastDate).Select(d => (Date: d, Recurrence: r)))
            .ToLookup(x => x.Date, x => x.Recurrence);

        var cells = _grid.Cells;
        var preview = new System.Text.StringBuilder();
        for (int i = 0; i < cells.Length; i++)
        {
            ref readonly var cell = ref cells[i];
            var all = new List<CellLine>();
            preview.Clear();

            foreach (var rec in recurrencesByDate[cell.Date])
            {
                all.Add(new CellLine("↻ " + rec.Title, rec.Color));
                preview.AppendLine($"↻ {rec.Title}  ({rec.Describe()})");
            }
            foreach (var memo in memosByDate[cell.Date])
            {
                all.Add(new CellLine(memo.Text.ReplaceLineEndings(" "), memo.Color));
                preview.AppendLine(memo.Text);
            }
            foreach (var todo in todosByDate[cell.Date])
            {
                all.Add(new CellLine("☐ " + todo.Text, 0));
                preview.AppendLine("☐ " + todo.Text);
            }

            IReadOnlyList<CellLine> lines = all.Count > MaxCellLines ? all.GetRange(0, MaxCellLines) : all;
            string? more = all.Count > MaxCellLines ? $"외 {all.Count - MaxCellLines}건" : null;
            Cells[i].Update(cell, FormatLunar(cell), lines, more, BuildPreview(cell, preview));
        }

        Title = $"{_year}년 {_month}월";
        DDayText = _dday?.Format(today);
    }

    /// <summary>칸에 마우스를 올렸을 때 보이는 전체 내용. 보여 줄 것이 없으면 null (툴팁 없음).</summary>
    private static string? BuildPreview(in DayCell cell, System.Text.StringBuilder body)
    {
        if (body.Length == 0)
            return null;

        var header = cell.Date.ToString("M월 d일 (ddd)", System.Globalization.CultureInfo.GetCultureInfo("ko-KR"));
        if (cell.HolidayName is not null)
            header += " · " + cell.HolidayName;
        if (cell.Lunar.IsValid)
            header += $" · 음 {cell.Lunar}";
        return header + Environment.NewLine + body.ToString().TrimEnd();
    }

    private string FormatLunar(in DayCell cell)
    {
        if (!cell.Lunar.IsValid)
            return "";
        return _settings.LunarDisplay switch
        {
            LunarDisplay.Daily => cell.Lunar.ToString(),
            LunarDisplay.Partial when cell.Date.Day == 1 || cell.Lunar.Day is 1 or 15 => cell.Lunar.ToString(),
            _ => "",
        };
    }

    [RelayCommand]
    private void PreviousMonth() => MoveMonth(-1);

    [RelayCommand]
    private void NextMonth() => MoveMonth(1);

    [RelayCommand]
    public void GoToday()
    {
        _year = _clock.Today.Year;
        _month = _clock.Today.Month;
        Refresh();
    }

    public void MoveMonth(int delta)
    {
        var first = new DateOnly(_year, _month, 1).AddMonths(delta);
        _year = first.Year;
        _month = first.Month;
        Refresh();
    }

    [RelayCommand]
    private void OpenSettings() => _shell.ShowSettings();

    [RelayCommand]
    private void Exit() => _shell.Shutdown();

    // ── 잠금 / 편집 모드 ───────────────────────────────

    // 편집기가 열려 있으면 잠그지 않는다 (Ctrl+클릭·🔒 버튼). 단축키·트레이는 ToggleLock으로 강제 전환
    private bool CanLock() => CanDragWindow;

    [RelayCommand(CanExecute = nameof(CanLock))]
    private void Lock() => SetLocked(true);

    private bool CanUnlock() => !IsEditMode;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private void Unlock() => SetLocked(false);

    [RelayCommand]
    private void ToggleLock() => SetLocked(IsEditMode);

    private void SetLocked(bool locked)
    {
        if (locked)
            CloseEditors(); // 열려 있던 편집기는 저장하지 않고 닫는다
        IsEditMode = !locked;
    }

    [RelayCommand]
    private void ToggleTodoPanel() => IsTodoPanelVisible = !IsTodoPanelVisible;

    // ── 메모 ──────────────────────────────────────────

    [RelayCommand]
    private void EditMemo(DayCellViewModel? cell)
    {
        if (cell is null || !IsEditMode)
            return;
        var memos = _memos.GetRange(cell.Date, cell.Date);
        MemoEditor = new MemoEditorViewModel(cell.Date, memos, CloseMemoEditor);
    }

    private void CloseMemoEditor(MemoEditorViewModel editor, bool save)
    {
        if (save)
        {
            _memos.ReplaceForDate(editor.Date, editor.Lines.Select(l => (l.Text, l.Color)));
            Refresh();
        }
        MemoEditor = null;
    }

    // ── 할일 ──────────────────────────────────────────

    private void ReloadTodos()
    {
        Todos.Clear();
        foreach (var item in _todos.GetAll())
            Todos.Add(new TodoItemViewModel(item, OnTodoChanged));
    }

    private void OnTodoChanged(TodoItemViewModel todo)
    {
        _todos.Update(todo.Item);
        if (todo.Due is not null)
            Refresh();
    }

    // CanExecute로 버튼을 막지 않는다: IME 조합 중에는 NewTodoText가 아직 비어 있을 수 있음
    [RelayCommand]
    private void AddTodo()
    {
        if (string.IsNullOrWhiteSpace(NewTodoText))
            return;

        DateOnly? due = NewTodoDue is { } d ? DateOnly.FromDateTime(d) : null;
        var item = _todos.Add(NewTodoText, due);

        // 미완료 항목 뒤, 완료 항목 앞에 넣는다
        int index = Todos.TakeWhile(t => !t.Done).Count();
        Todos.Insert(index, new TodoItemViewModel(item, OnTodoChanged));

        NewTodoText = "";
        NewTodoDue = _clock.Today.ToDateTime(TimeOnly.MinValue);
        if (due is not null)
            Refresh();
    }

    [RelayCommand]
    private void DeleteTodo(TodoItemViewModel? todo)
    {
        if (todo is null)
            return;
        _todos.Delete(todo.Id);
        Todos.Remove(todo);
        if (todo.Due is not null)
            Refresh();
    }

    [RelayCommand]
    private void BeginEditTodo(TodoItemViewModel? todo)
    {
        if (todo is null || !IsEditMode)
            return;
        foreach (var other in Todos)
            other.IsEditing = false; // 한 번에 하나만
        todo.BeginEdit();
    }

    [RelayCommand]
    private void CommitEditTodo(TodoItemViewModel? todo)
    {
        if (todo?.GetEdited() is not { } edited)
            return; // 내용이 비면 편집 상태 유지
        bool affectsCalendar = todo.Due is not null || edited.Due is not null;
        _todos.Update(edited);
        todo.Apply(edited);
        if (affectsCalendar)
            Refresh();
    }

    [RelayCommand]
    private void CancelEditTodo(TodoItemViewModel? todo)
    {
        if (todo is not null)
            todo.IsEditing = false;
    }

    [RelayCommand]
    private void MoveTodoUp(TodoItemViewModel? todo) => MoveTodo(todo, -1);

    [RelayCommand]
    private void MoveTodoDown(TodoItemViewModel? todo) => MoveTodo(todo, 1);

    /// <summary>같은 그룹(미완료/완료) 안에서만 한 칸 이동하고 순서를 저장한다.</summary>
    private void MoveTodo(TodoItemViewModel? todo, int delta)
    {
        if (todo is null)
            return;
        int from = Todos.IndexOf(todo);
        int to = from + delta;
        if (from < 0 || to < 0 || to >= Todos.Count || Todos[to].Done != todo.Done)
            return;

        Todos.Move(from, to);
        _todos.Reorder(Todos.Select(t => t.Id).ToList());
    }

    // ── D-day ─────────────────────────────────────────

    [RelayCommand]
    private void EditDDay()
    {
        if (IsEditMode)
            DDayEditor = new DDayEditorViewModel(_dday, _clock.Today, CloseDDayEditor);
    }

    private void CloseDDayEditor(DDayEditorViewModel editor, DDayEditorResult result)
    {
        switch (result)
        {
            case DDayEditorResult.Save:
                _dday = _ddays.Save(editor.ToModel());
                break;
            case DDayEditorResult.Delete when _dday is not null:
                _ddays.Delete(_dday.Id);
                _dday = null;
                break;
        }
        DDayEditor = null;
        DDayText = _dday?.Format(_clock.Today);
    }

    // ── 반복 일정 ─────────────────────────────────────

    /// <summary>헤더 ↻ 버튼(오늘 기준) 또는 메모 편집기(그 날짜 기준)에서 연다.</summary>
    [RelayCommand]
    private void OpenRecurrences(object? anchor)
    {
        if (!IsEditMode)
            return;
        var date = anchor switch
        {
            DateOnly d => d,
            MemoEditorViewModel memo => memo.Date,
            _ => _clock.Today,
        };
        RecurrenceEditor = new RecurrenceEditorViewModel(
            _recurrences,
            date,
            save: r =>
            {
                var saved = _recurrenceRepo.Save(r);
                _recurrences.Add(saved);
                Refresh();
                return saved;
            },
            delete: r =>
            {
                _recurrenceRepo.Delete(r.Id);
                _recurrences.RemoveAll(x => x.Id == r.Id);
                Refresh();
            },
            close: _ => RecurrenceEditor = null);
    }

    /// <summary>잠금 전환 시 열려 있는 편집기를 저장하지 않고 닫는다.</summary>
    public void CloseEditors()
    {
        MemoEditor = null;
        DDayEditor = null;
        RecurrenceEditor = null;
        foreach (var todo in Todos)
            todo.IsEditing = false;
    }

    /// <summary>가져오기 후 전체 재로딩.</summary>
    public void ReloadAll()
    {
        ReloadTodos();
        _dday = _ddays.GetAll().FirstOrDefault();
        _recurrences = _recurrenceRepo.GetAll();
        Refresh();
    }
}
