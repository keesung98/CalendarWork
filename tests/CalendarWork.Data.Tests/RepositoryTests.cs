using CalendarWork.Core.Models;

namespace CalendarWork.Data.Tests;

public class RepositoryTests : IDisposable
{
    private readonly CalendarDatabase _db = CalendarDatabase.InMemory();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Memo_ReplaceAndQueryRange()
    {
        var repo = new MemoRepository(_db);
        var date = new DateOnly(2026, 10, 12);
        repo.ReplaceForDate(date, [("회의", 1), ("  ", 0), ("점심 약속", 2)]);
        repo.ReplaceForDate(new DateOnly(2026, 12, 1), [("범위 밖", 0)]);

        var memos = repo.GetRange(new DateOnly(2026, 9, 27), new DateOnly(2026, 11, 7));
        Assert.Equal(["회의", "점심 약속"], memos.Select(m => m.Text));
        Assert.Equal([1, 2], memos.Select(m => m.Color));

        repo.ReplaceForDate(date, []);
        Assert.Empty(repo.GetRange(date, date));
    }

    [Fact]
    public void Todo_Crud()
    {
        var repo = new TodoRepository(_db);
        var a = repo.Add("보고서", new DateOnly(2026, 10, 20));
        var b = repo.Add("장보기", null);

        repo.Update(a with { Done = true });
        var all = repo.GetAll();
        Assert.Equal([b.Id, a.Id], all.Select(t => t.Id)); // 미완료 먼저
        Assert.True(all.Single(t => t.Id == a.Id).Done);
        Assert.Null(all.Single(t => t.Id == b.Id).Due);

        repo.Delete(a.Id);
        Assert.Single(repo.GetAll());
    }

    [Fact]
    public void DDay_Upsert()
    {
        var repo = new DDayRepository(_db);
        var saved = repo.Save(new DDay(0, "기말고사", new DateOnly(2026, 11, 6), false));
        Assert.NotEqual(0, saved.Id);

        repo.Save(saved with { Title = "기말" });
        var loaded = Assert.Single(repo.GetAll());
        Assert.Equal("기말", loaded.Title);
        Assert.Equal("D-25 기말", loaded.Format(new DateOnly(2026, 10, 12)));
    }

    [Fact]
    public void Backup_RoundTrip()
    {
        new MemoRepository(_db).ReplaceForDate(new DateOnly(2026, 10, 12), [("메모", 3)]);
        new TodoRepository(_db).Add("할일", new DateOnly(2026, 10, 13));
        new DDayRepository(_db).Save(new DDay(0, "여행", new DateOnly(2027, 1, 1), false));
        new RecurrenceRepository(_db).Save(new Recurrence(0, "제사", RecurrenceRule.LunarYearly, new DateOnly(2026, 3, 1), 1));

        var backup = new BackupService(_db);
        using var ms = new MemoryStream();
        backup.Export(ms);

        using var other = CalendarDatabase.InMemory();
        ms.Position = 0;
        new BackupService(other).Import(ms);

        Assert.Equal("메모", Assert.Single(new MemoRepository(other).GetRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31))).Text);
        Assert.Equal(new DateOnly(2026, 10, 13), Assert.Single(new TodoRepository(other).GetAll()).Due);
        Assert.Equal("여행", Assert.Single(new DDayRepository(other).GetAll()).Title);
        Assert.Equal(RecurrenceRule.LunarYearly, Assert.Single(new RecurrenceRepository(other).GetAll()).Rule);
    }
}

public class RecurrenceAndOrderTests : IDisposable
{
    private readonly CalendarDatabase _db = CalendarDatabase.InMemory();

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Recurrence_Upsert()
    {
        var repo = new RecurrenceRepository(_db);
        var saved = repo.Save(new Recurrence(0, " 회의 ", RecurrenceRule.Weekly, new DateOnly(2026, 10, 7), 2));
        Assert.Equal("회의", saved.Title);

        repo.Save(saved with { Rule = RecurrenceRule.Monthly });
        var loaded = Assert.Single(repo.GetAll());
        Assert.Equal(RecurrenceRule.Monthly, loaded.Rule);
        Assert.Equal(2, loaded.Color);

        repo.Delete(saved.Id);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Todo_Reorder()
    {
        var repo = new TodoRepository(_db);
        var a = repo.Add("A", null);
        var b = repo.Add("B", null);
        var c = repo.Add("C", null);

        repo.Reorder([c.Id, a.Id, b.Id]);
        Assert.Equal(["C", "A", "B"], repo.GetAll().Select(t => t.Text));
    }
}
