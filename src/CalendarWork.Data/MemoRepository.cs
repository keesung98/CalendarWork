using CalendarWork.Core.Calendar;
using CalendarWork.Core.Models;
using Microsoft.Data.Sqlite;

namespace CalendarWork.Data;

public sealed class MemoRepository
{
    private readonly CalendarDatabase _db;
    private readonly SqliteCommand _selectRange;
    private readonly SqliteCommand _deleteDate;
    private readonly SqliteCommand _insert;

    public MemoRepository(CalendarDatabase db)
    {
        _db = db;
        _selectRange = db.Prepare(
            "SELECT id, date, text, color, sort FROM memo WHERE date BETWEEN $from AND $to ORDER BY date, sort, id;",
            "$from", "$to");
        _deleteDate = db.Prepare("DELETE FROM memo WHERE date = $date;", "$date");
        _insert = db.Prepare(
            "INSERT INTO memo (date, text, color, sort, updated) VALUES ($date, $text, $color, $sort, $updated);",
            "$date", "$text", "$color", "$sort", "$updated");
    }

    /// <summary>그리드 시작~끝 날짜의 메모를 한 번에 조회.</summary>
    public List<Memo> GetRange(DateOnly from, DateOnly to)
    {
        _selectRange.Parameters["$from"].Value = DateKey.From(from);
        _selectRange.Parameters["$to"].Value = DateKey.From(to);

        var result = new List<Memo>();
        using var reader = _selectRange.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Memo(
                reader.GetInt64(0),
                DateKey.ToDate(reader.GetInt32(1)),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4)));
        }
        return result;
    }

    /// <summary>해당 날짜의 메모를 통째로 교체 (편집 종료 시 1회 호출).</summary>
    public void ReplaceForDate(DateOnly date, IEnumerable<(string Text, int Color)> memos)
    {
        int key = DateKey.From(date);
        using var tx = _db.Connection.BeginTransaction();
        _deleteDate.Transaction = tx;
        _insert.Transaction = tx;

        _deleteDate.Parameters["$date"].Value = key;
        _deleteDate.ExecuteNonQuery();

        int sort = 0;
        long now = CalendarDatabase.Now();
        foreach (var (text, color) in memos)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            _insert.Parameters["$date"].Value = key;
            _insert.Parameters["$text"].Value = text.Trim();
            _insert.Parameters["$color"].Value = Math.Clamp(color, 0, MemoColors.Count - 1);
            _insert.Parameters["$sort"].Value = sort++;
            _insert.Parameters["$updated"].Value = now;
            _insert.ExecuteNonQuery();
        }

        tx.Commit();
        _deleteDate.Transaction = null;
        _insert.Transaction = null;
    }
}
