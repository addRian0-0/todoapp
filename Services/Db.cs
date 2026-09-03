using Microsoft.Data.Sqlite;
using Notitas.Models;
using System.IO;

namespace Notitas.Services;

public static class Db
{
    private static string _dataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Notitas");

    public static string DataDir => _dataDir;

    public static string DbPath => Path.Combine(_dataDir, "notitas.db");

    /// <summary>
    /// Aparta los datos a una subcarpeta. Lo usa --selftest para no tocar jamás
    /// las notas reales del usuario.
    /// </summary>
    public static void UseIsolatedDataDir(string subfolder)
    {
        _dataDir = Path.Combine(_dataDir, subfolder);
        Directory.CreateDirectory(_dataDir);
    }

    private static string ConnStr => $"Data Source={DbPath}";

    public static void Init()
    {
        Directory.CreateDirectory(DataDir);
        using var c = Open();
        Exec(c, """
            CREATE TABLE IF NOT EXISTS subjects(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                color TEXT NOT NULL DEFAULT '#8B5CF6',
                icon TEXT NOT NULL DEFAULT '',
                archived INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS notes(
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                subject_id INTEGER NULL REFERENCES subjects(id) ON DELETE SET NULL,
                title TEXT NOT NULL,
                content_xaml TEXT NOT NULL DEFAULT '',
                preview TEXT NOT NULL DEFAULT '',
                archived INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL);
            """);
        Log.Info("Base de datos inicializada");
    }

    private static SqliteConnection Open()
    {
        var c = new SqliteConnection(ConnStr);
        c.Open();
        using var pragma = c.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON";
        pragma.ExecuteNonQuery();
        return c;
    }

    private static void Exec(SqliteConnection c, string sql, params (string, object?)[] p)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in p) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ---- Subjects ----
    public static List<Subject> GetSubjects(bool archived = false)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,name,color,icon,archived FROM subjects WHERE archived=@a ORDER BY name";
        cmd.Parameters.AddWithValue("@a", archived ? 1 : 0);
        var list = new List<Subject>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Subject { Id = r.GetInt64(0), Name = r.GetString(1), ColorHex = r.GetString(2), Icon = r.GetString(3), Archived = r.GetInt64(4) == 1 });
        return list;
    }

    public static long AddSubject(Subject s)
    {
        using var c = Open();
        Exec(c, "INSERT INTO subjects(name,color,icon) VALUES(@n,@c,@i)", ("@n", s.Name), ("@c", s.ColorHex), ("@i", s.Icon));
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return (long)cmd.ExecuteScalar()!;
    }

    public static void UpdateSubject(Subject s)
    {
        using var c = Open();
        Exec(c, "UPDATE subjects SET name=@n,color=@c,icon=@i,archived=@a WHERE id=@id",
            ("@n", s.Name), ("@c", s.ColorHex), ("@i", s.Icon), ("@a", s.Archived ? 1 : 0), ("@id", s.Id));
    }

    public static void DeleteSubject(long id)
    {
        using var c = Open();
        // desvincular explícitamente por si la BD se creó sin foreign_keys activo
        Exec(c, "UPDATE notes SET subject_id=NULL WHERE subject_id=@id", ("@id", id));
        Exec(c, "DELETE FROM subjects WHERE id=@id", ("@id", id));
    }

    // ---- Notes ----
    public static List<Note> GetNotes(long? subjectId, bool archived, string? search = null)
    {
        using var c = Open();
        using var cmd = c.CreateCommand();
        var sql = "SELECT id,subject_id,title,content_xaml,preview,archived,created_at,updated_at FROM notes WHERE archived=@a";
        cmd.Parameters.AddWithValue("@a", archived ? 1 : 0);
        if (subjectId is not null) { sql += " AND subject_id=@s"; cmd.Parameters.AddWithValue("@s", subjectId); }
        if (!string.IsNullOrWhiteSpace(search)) { sql += " AND (title LIKE @q OR preview LIKE @q)"; cmd.Parameters.AddWithValue("@q", $"%{search}%"); }
        sql += " ORDER BY updated_at DESC";
        cmd.CommandText = sql;
        var list = new List<Note>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Note
            {
                Id = r.GetInt64(0),
                SubjectId = r.IsDBNull(1) ? null : r.GetInt64(1),
                Title = r.GetString(2),
                ContentXaml = r.GetString(3),
                Preview = r.GetString(4),
                Archived = r.GetInt64(5) == 1,
                CreatedAt = DateTime.Parse(r.GetString(6)),
                UpdatedAt = DateTime.Parse(r.GetString(7)),
            });
        return list;
    }

    public static long AddNote(Note n)
    {
        using var c = Open();
        Exec(c, "INSERT INTO notes(subject_id,title,content_xaml,preview,created_at,updated_at) VALUES(@s,@t,@x,@p,@c,@u)",
            ("@s", n.SubjectId), ("@t", n.Title), ("@x", n.ContentXaml), ("@p", n.Preview),
            ("@c", n.CreatedAt.ToString("o")), ("@u", n.UpdatedAt.ToString("o")));
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return (long)cmd.ExecuteScalar()!;
    }

    public static void UpdateNote(Note n)
    {
        n.UpdatedAt = DateTime.Now;
        using var c = Open();
        Exec(c, "UPDATE notes SET subject_id=@s,title=@t,content_xaml=@x,preview=@p,archived=@a,updated_at=@u WHERE id=@id",
            ("@s", n.SubjectId), ("@t", n.Title), ("@x", n.ContentXaml), ("@p", n.Preview),
            ("@a", n.Archived ? 1 : 0), ("@u", n.UpdatedAt.ToString("o")), ("@id", n.Id));
    }

    public static void DeleteNote(long id)
    {
        using var c = Open();
        Exec(c, "DELETE FROM notes WHERE id=@id", ("@id", id));
    }
}
