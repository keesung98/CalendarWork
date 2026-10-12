using CalendarWork.Core.Calendar;
using CalendarWork.Core.Models;
using Microsoft.Data.Sqlite;

namespace CalendarWork.Data;

public sealed class DDayRepository
{
    private readonly SqliteCommand _selectAll;
    private readonly SqliteCommand _upsert;
    private readonly SqliteCommand _delete;

    public DDayRepository(CalendarDatabase db)
    {
        _selectAll = db.Prepare("SELECT id, title, target, lunar FROM dday ORDER BY target, id;");
        _upsert = db.Prepare(
            """
            INSERT INTO dday (id, title, target, lunar) VALUES ($id, $title, $target, $lunar)
            ON CONFLICT(id) DO UPDATE SET title = excluded.title, target = excluded.target, lunar = excluded.lunar
            RETURNING id;
            """,
            "$id", "$title", "$target", "$lunar");
        _delete = db.Prepare("DELETE FROM dday WHERE id = $id;", "$id");
    }

    public List<DDay> GetAll()
    {
        var result = new List<DDay>();
        using var reader = _selectAll.ExecuteReader();
        while (reader.Read())
            result.Add(new DDay(reader.GetInt64(0), reader.GetString(1), DateKey.ToDate(reader.GetInt32(2)), reader.GetInt32(3) != 0));
        return result;
    }

    /// <summary>Id가 0이면 새로 추가, 아니면 갱신. 저장된 항목을 반환한다.</summary>
    public DDay Save(DDay dday)
    {
        _upsert.Parameters["$id"].Value = dday.Id == 0 ? DBNull.Value : dday.Id;
        _upsert.Parameters["$title"].Value = dday.Title.Trim();
        _upsert.Parameters["$target"].Value = DateKey.From(dday.Target);
        _upsert.Parameters["$lunar"].Value = dday.Lunar ? 1 : 0;
        long id = Convert.ToInt64(_upsert.ExecuteScalar());
        return dday with { Id = id, Title = dday.Title.Trim() };
    }

    public void Delete(long id)
    {
        _delete.Parameters["$id"].Value = id;
        _delete.ExecuteNonQuery();
    }
}
