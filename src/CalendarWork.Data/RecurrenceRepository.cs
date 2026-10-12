using CalendarWork.Core.Calendar;
using CalendarWork.Core.Models;
using Microsoft.Data.Sqlite;

namespace CalendarWork.Data;

public sealed class RecurrenceRepository
{
    private readonly SqliteCommand _selectAll;
    private readonly SqliteCommand _upsert;
    private readonly SqliteCommand _delete;

    public RecurrenceRepository(CalendarDatabase db)
    {
        _selectAll = db.Prepare("SELECT id, title, rule, anchor, color FROM recurrence ORDER BY anchor, id;");
        _upsert = db.Prepare(
            """
            INSERT INTO recurrence (id, title, rule, anchor, color) VALUES ($id, $title, $rule, $anchor, $color)
            ON CONFLICT(id) DO UPDATE SET title = excluded.title, rule = excluded.rule, anchor = excluded.anchor, color = excluded.color
            RETURNING id;
            """,
            "$id", "$title", "$rule", "$anchor", "$color");
        _delete = db.Prepare("DELETE FROM recurrence WHERE id = $id;", "$id");
    }

    /// <summary>반복 일정은 수가 적으므로 전부 메모리에 올려 두고 월 이동 시 계산한다.</summary>
    public List<Recurrence> GetAll()
    {
        var result = new List<Recurrence>();
        using var reader = _selectAll.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new Recurrence(
                reader.GetInt64(0),
                reader.GetString(1),
                (RecurrenceRule)reader.GetInt32(2),
                DateKey.ToDate(reader.GetInt32(3)),
                reader.GetInt32(4)));
        }
        return result;
    }

    /// <summary>Id가 0이면 새로 추가, 아니면 갱신.</summary>
    public Recurrence Save(Recurrence recurrence)
    {
        _upsert.Parameters["$id"].Value = recurrence.Id == 0 ? DBNull.Value : recurrence.Id;
        _upsert.Parameters["$title"].Value = recurrence.Title.Trim();
        _upsert.Parameters["$rule"].Value = (int)recurrence.Rule;
        _upsert.Parameters["$anchor"].Value = DateKey.From(recurrence.Anchor);
        _upsert.Parameters["$color"].Value = Math.Clamp(recurrence.Color, 0, MemoColors.Count - 1);
        long id = Convert.ToInt64(_upsert.ExecuteScalar());
        return recurrence with { Id = id, Title = recurrence.Title.Trim() };
    }

    public void Delete(long id)
    {
        _delete.Parameters["$id"].Value = id;
        _delete.ExecuteNonQuery();
    }
}
