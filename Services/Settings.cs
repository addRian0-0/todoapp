using Notitas.Models;
using System.IO;
using System.Text.Json;

namespace Notitas.Services;

public static class Settings
{
    private static string SettingsPath => Path.Combine(Db.DataDir, "settings.json");
    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch (Exception ex) { Log.Warn($"No se pudo leer settings.json: {ex.Message}"); }
        Log.MinLevel = Current.LogLevel;
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Db.DataDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { Log.Error($"No se pudo guardar settings.json: {ex.Message}"); }
    }
}
