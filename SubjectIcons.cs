namespace Notitas;

/// <summary>
/// Catálogo de iconos para materias. Se guarda la clave en la base de datos y se
/// dibuja el vector correspondiente: así el icono no depende de fuentes de emoji.
/// </summary>
public static class SubjectIcons
{
    public static readonly (string Key, string Label)[] All =
    {
        ("book",     "General"),
        ("math",     "Matemáticas"),
        ("flask",    "Química"),
        ("dna",      "Biología"),
        ("history",  "Historia"),
        ("globe",    "Geografía"),
        ("language", "Idiomas"),
        ("code",     "Informática"),
        ("palette",  "Arte"),
        ("music",    "Música"),
        ("ball",     "Deporte"),
        ("pencil",   "Tareas"),
    };

    public static bool IsKnown(string? key) =>
        !string.IsNullOrEmpty(key) && All.Any(i => i.Key == key);

    /// <summary>Clave del recurso StreamGeometry en Icons.xaml.</summary>
    public static string GeometryKey(string key) => key switch
    {
        "math" => "SubjMath",
        "flask" => "SubjFlask",
        "dna" => "SubjDna",
        "history" => "SubjHistory",
        "globe" => "SubjGlobe",
        "language" => "SubjLanguage",
        "code" => "SubjCode",
        "palette" => "SubjPalette",
        "music" => "SubjMusic",
        "ball" => "SubjBall",
        "pencil" => "SubjPencil",
        _ => "SubjBook",
    };
}
