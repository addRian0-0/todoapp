namespace Notitas.Models;

public class Subject
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "#8B5CF6";
    public string Icon { get; set; } = ""; // Segoe MDL2 book-ish glyph
    public bool Archived { get; set; }
}

public class Note
{
    public long Id { get; set; }
    public long? SubjectId { get; set; }
    public string Title { get; set; } = "Nota sin título";
    /// <summary>FlowDocument serialized as XAML string (internal format).</summary>
    public string ContentXaml { get; set; } = "";
    public string Preview { get; set; } = "";
    public bool Archived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class AppSettings
{
    public string Theme { get; set; } = "Claro"; // Claro | Oscuro
    public string AccentHex { get; set; } = "#8B5CF6";
    public string ExportFolder { get; set; } =
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public string LogLevel { get; set; } = "Info";

    /// <summary>Escala de toda la interfaz (0.8 a 1.4).</summary>
    public double AppZoom { get; set; } = 1.0;

    /// <summary>Escala del contenido de la nota abierta (0.7 a 2.5).</summary>
    public double NoteZoom { get; set; } = 1.0;
}
