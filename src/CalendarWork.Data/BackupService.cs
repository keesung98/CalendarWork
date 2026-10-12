using System.Text.Json;
using System.Text.Json.Serialization;
using CalendarWork.Core.Calendar;

namespace CalendarWork.Data;

public sealed class BackupDocument
{
    public int Format { get; set; } = 1;
    public DateTimeOffset ExportedAt { get; set; }
    public List<BackupMemo> Memos { get; set; } = [];
    public List<BackupTodo> Todos { get; set; } = [];
    public List<BackupDDay> DDays { get; set; } = [];

    /// <summary>반복 일정. 이전 백업 파일에는 없으므로 비어 있을 수 있다.</summary>
    public List<BackupRecurrence> Recurrences { get; set; } = [];
}

public sealed record BackupMemo(DateOnly Date, string Text, int Color, int Sort);
public sealed record BackupTodo(string Text, DateOnly? Due, bool Done, int Sort);
public sealed record BackupDDay(string Title, DateOnly Target, bool Lunar);
public sealed record BackupRecurrence(string Title, int Rule, DateOnly Anchor, int Color);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(BackupDocument))]
public sealed partial class BackupJsonContext : JsonSerializerContext;

/// <summary>데이터 내보내기/가져오기 (JSON). 가져오기는 기존 데이터를 전부 교체한다.</summary>
public sealed class BackupService(CalendarDatabase db)
{
    public void Export(Stream stream)
    {
        var doc = new BackupDocument { ExportedAt = DateTimeOffset.Now };
        var conn = db.Connection;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT date, text, color, sort FROM memo ORDER BY date, sort;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                doc.Memos.Add(new BackupMemo(DateKey.ToDate(r.GetInt32(0)), r.GetString(1), r.GetInt32(2), r.GetInt32(3)));
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT text, due, done, sort FROM todo ORDER BY sort;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                doc.Todos.Add(new BackupTodo(r.GetString(0), r.IsDBNull(1) ? null : DateKey.ToDate(r.GetInt32(1)), r.GetInt32(2) != 0, r.GetInt32(3)));
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT title, target, lunar FROM dday ORDER BY id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                doc.DDays.Add(new BackupDDay(r.GetString(0), DateKey.ToDate(r.GetInt32(1)), r.GetInt32(2) != 0));
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT title, rule, anchor, color FROM recurrence ORDER BY id;";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                doc.Recurrences.Add(new BackupRecurrence(r.GetString(0), r.GetInt32(1), DateKey.ToDate(r.GetInt32(2)), r.GetInt32(3)));
        }

        JsonSerializer.Serialize(stream, doc, BackupJsonContext.Default.BackupDocument);
    }

    public void Import(Stream stream)
    {
        var doc = JsonSerializer.Deserialize(stream, BackupJsonContext.Default.BackupDocument)
                  ?? throw new InvalidDataException("백업 파일이 비어 있습니다.");
        long now = CalendarDatabase.Now();
        var conn = db.Connection;

        using var tx = conn.BeginTransaction();
        db.Execute("DELETE FROM memo; DELETE FROM todo; DELETE FROM dday; DELETE FROM recurrence;", tx);

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO memo (date, text, color, sort, updated) VALUES ($d, $t, $c, $s, $u);";
            foreach (var m in doc.Memos)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$d", DateKey.From(m.Date));
                cmd.Parameters.AddWithValue("$t", m.Text);
                cmd.Parameters.AddWithValue("$c", m.Color);
                cmd.Parameters.AddWithValue("$s", m.Sort);
                cmd.Parameters.AddWithValue("$u", now);
                cmd.ExecuteNonQuery();
            }
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO todo (text, due, done, sort, updated) VALUES ($t, $d, $done, $s, $u);";
            foreach (var t in doc.Todos)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$t", t.Text);
                cmd.Parameters.AddWithValue("$d", t.Due is { } due ? DateKey.From(due) : DBNull.Value);
                cmd.Parameters.AddWithValue("$done", t.Done ? 1 : 0);
                cmd.Parameters.AddWithValue("$s", t.Sort);
                cmd.Parameters.AddWithValue("$u", now);
                cmd.ExecuteNonQuery();
            }
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO dday (title, target, lunar) VALUES ($t, $d, $l);";
            foreach (var d in doc.DDays)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$t", d.Title);
                cmd.Parameters.AddWithValue("$d", DateKey.From(d.Target));
                cmd.Parameters.AddWithValue("$l", d.Lunar ? 1 : 0);
                cmd.ExecuteNonQuery();
            }
        }
        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO recurrence (title, rule, anchor, color) VALUES ($t, $r, $a, $c);";
            foreach (var rec in doc.Recurrences)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("$t", rec.Title);
                cmd.Parameters.AddWithValue("$r", rec.Rule);
                cmd.Parameters.AddWithValue("$a", DateKey.From(rec.Anchor));
                cmd.Parameters.AddWithValue("$c", rec.Color);
                cmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }
}
