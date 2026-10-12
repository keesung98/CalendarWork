using CalendarWork.Core.Calendar;
using CalendarWork.Core.Models;
using Microsoft.Data.Sqlite;

namespace CalendarWork.Data;

public sealed class TodoRepository
{
    private readonly SqliteCommand _selectAll;
    private readonly SqliteCommand _insert;
    private readonly SqliteCommand _update;
    private readonly SqliteCommand _delete;
    private readonly SqliteCommand _reorder;
    private readonly CalendarDatabase _db;

    public TodoRepository(CalendarDatabase db)
    {
        _db = db;
        _reorder = db.Prepare("UPDATE todo SET sort = $sort WHERE id = $id;", "$sort", "$id");
        _selectAll = db.Prepare("SELECT id, text, due, done, sort FROM todo ORDER BY done, sort, id;");
        _insert = db.Prepare(
            "INSERT INTO todo (text, due, done, sort, updated) VALUES ($text, $due, 0, (SELECT IFNULL(MAX(sort), 0) + 1 FROM todo), $updated) RETURNING id, sort;",
            "$text", "$due", "$updated");
        _update = db.Prepare(
            "UPDATE todo SET text = $text, due = $due, done = $done, updated = $updated WHERE id = $id;",
            "$text", "$due", "$done", "$updated", "$id");
        _delete = db.Prepare("DELETE FROM todo WHERE id = $id;", "$id");
    }

    public List<TodoItem> GetAll()
    {
        var result = new List<TodoItem>();
        using var reader = _selectAll.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new TodoItem(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : DateKey.ToDate(reader.GetInt32(2)),
                reader.GetInt32(3) != 0,
                reader.GetInt32(4)));
        }
        return result;
    }

    public TodoItem Add(string text, DateOnly? due)
    {
        _insert.Parameters["$text"].Value = text.Trim();
        _insert.Parameters["$due"].Value = due is { } d ? DateKey.From(d) : DBNull.Value;
        _insert.Parameters["$updated"].Value = CalendarDatabase.Now();

        using var reader = _insert.ExecuteReader();
        reader.Read();
        return new TodoItem(reader.GetInt64(0), text.Trim(), due, false, reader.GetInt32(1));
    }

    public void Update(TodoItem item)
    {
        _update.Parameters["$text"].Value = item.Text;
        _update.Parameters["$due"].Value = item.Due is { } d ? DateKey.From(d) : DBNull.Value;
        _update.Parameters["$done"].Value = item.Done ? 1 : 0;
        _update.Parameters["$updated"].Value = CalendarDatabase.Now();
        _update.Parameters["$id"].Value = item.Id;
        _update.ExecuteNonQuery();
    }

    public void Delete(long id)
    {
        _delete.Parameters["$id"].Value = id;
        _delete.ExecuteNonQuery();
    }

    /// <summary>주어진 순서대로 sort를 0, 1, 2 … 로 다시 매긴다.</summary>
    public void Reorder(IReadOnlyList<long> idsInOrder)
    {
        using var tx = _db.Connection.BeginTransaction();
        _reorder.Transaction = tx;
        for (int i = 0; i < idsInOrder.Count; i++)
        {
            _reorder.Parameters["$sort"].Value = i;
            _reorder.Parameters["$id"].Value = idsInOrder[i];
            _reorder.ExecuteNonQuery();
        }
        tx.Commit();
        _reorder.Transaction = null;
    }
}
