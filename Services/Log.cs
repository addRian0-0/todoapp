using System.IO;

namespace Notitas.Services;

public record LogEntry(DateTime Time, string Level, string Message);

public static class Log
{
    // se calculan al vuelo porque --selftest cambia la carpeta de datos al arrancar
    public static string LogDir => Path.Combine(Db.DataDir, "logs");
    private static string LogFile => Path.Combine(LogDir, $"notitas-{DateTime.Now:yyyyMMdd}.log");
    private static readonly object _lock = new();

    public static List<LogEntry> Recent { get; } = new();
    public static event Action? Changed;

    public static string MinLevel { get; set; } = "Info";
    private static int Rank(string l) => l switch { "Debug" => 0, "Info" => 1, "Warn" => 2, "Error" => 3, _ => 1 };

    public static void Debug(string m) => Write("Debug", m);
    public static void Info(string m) => Write("Info", m);
    public static void Warn(string m) => Write("Warn", m);
    public static void Error(string m) => Write("Error", m);

    private static void Write(string level, string msg)
    {
        if (Rank(level) < Rank(MinLevel)) return;
        var e = new LogEntry(DateTime.Now, level, msg);
        lock (_lock)
        {
            Recent.Add(e);
            if (Recent.Count > 200) Recent.RemoveAt(0);
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(LogFile, $"{e.Time:HH:mm:ss} [{level}] {msg}{Environment.NewLine}");
            }
            catch { /* logging must never crash the app */ }
        }
        Changed?.Invoke();
    }
}
