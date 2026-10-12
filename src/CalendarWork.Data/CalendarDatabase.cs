using Microsoft.Data.Sqlite;

namespace CalendarWork.Data;

/// <summary>
/// 앱 수명 동안 SQLite 연결 1개를 유지한다. WAL 모드, user_version 기반 마이그레이션.
/// </summary>
public sealed class CalendarDatabase : IDisposable
{
    private const int CurrentVersion = 1;

    public CalendarDatabase(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        Connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        Connection.Open();
        Execute("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON;");
        Migrate();
    }

    /// <summary>테스트용 인메모리 DB.</summary>
    public static CalendarDatabase InMemory() => new(":memory:");

    internal SqliteConnection Connection { get; }

    internal SqliteCommand Prepare(string sql, params string[] parameterNames)
    {
        var cmd = Connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var name in parameterNames)
            cmd.Parameters.Add(new SqliteParameter(name, null));
        cmd.Prepare();
        return cmd;
    }

    internal void Execute(string sql, SqliteTransaction? tx = null)
    {
        using var cmd = Connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    internal static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private void Migrate()
    {
        using var get = Connection.CreateCommand();
        get.CommandText = "PRAGMA user_version;";
        int version = Convert.ToInt32(get.ExecuteScalar());
        if (version >= CurrentVersion)
            return;

        using var tx = Connection.BeginTransaction();
        if (version < 1)
        {
            Execute("""
                CREATE TABLE memo (
                  id        INTEGER PRIMARY KEY,
                  date      INTEGER NOT NULL,
                  text      TEXT    NOT NULL,
                  color     INTEGER NOT NULL DEFAULT 0,
                  sort      INTEGER NOT NULL DEFAULT 0,
                  updated   INTEGER NOT NULL
                );
                CREATE INDEX ix_memo_date ON memo(date);

                CREATE TABLE todo (
                  id        INTEGER PRIMARY KEY,
                  text      TEXT    NOT NULL,
                  due       INTEGER,
                  done      INTEGER NOT NULL DEFAULT 0,
                  sort      INTEGER NOT NULL DEFAULT 0,
                  updated   INTEGER NOT NULL
                );
                CREATE INDEX ix_todo_due ON todo(due) WHERE due IS NOT NULL;

                CREATE TABLE dday (
                  id INTEGER PRIMARY KEY, title TEXT NOT NULL, target INTEGER NOT NULL, lunar INTEGER NOT NULL DEFAULT 0
                );

                CREATE TABLE recurrence (
                  id INTEGER PRIMARY KEY, title TEXT NOT NULL,
                  rule INTEGER NOT NULL,
                  anchor INTEGER NOT NULL, color INTEGER NOT NULL DEFAULT 0
                );
                """, tx);
        }
        Execute($"PRAGMA user_version = {CurrentVersion};", tx);
        tx.Commit();
    }

    public void Dispose() => Connection.Dispose();
}
