using Notitas.Services;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Notitas;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error($"Excepción no controlada: {ex.Exception.Message}");
            MessageBox.Show(ex.Exception.Message, "Notitas - Error", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
        bool selfTest = e.Args.Contains("--selftest");
        if (selfTest)
        {
            // el modo de prueba trabaja en su propia carpeta: nunca toca las notas reales
            Db.UseIsolatedDataDir("selftest-data");
            try { if (File.Exists(Db.DbPath)) File.Delete(Db.DbPath); } catch { }
        }

        Db.Init();
        Settings.Load();
        ApplyTheme(Services.Settings.Current.Theme);
        ApplyAccent(Services.Settings.Current.AccentHex);
        SeedWelcomeNote();
        Log.Info("Aplicación iniciada correctamente");

        if (selfTest)
        {
            var code = SelfTest.Run(e.Args);
            Shutdown(code);
            return;
        }
        new MainWindow().Show();
    }

    private static void SeedWelcomeNote()
    {
        if (Db.GetNotes(null, false).Count > 0 || Db.GetNotes(null, true).Count > 0
            || Db.GetSubjects().Count > 0 || Db.GetSubjects(true).Count > 0) return;

        const string ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var xaml = $"""
            <Section xmlns="{ns}">
              <Paragraph FontSize="20" FontWeight="SemiBold">Bienvenido a Notitas</Paragraph>
              <Paragraph>Tu app local de apuntes. Algunas cosas que puedes hacer:</Paragraph>
              <List>
                <ListItem><Paragraph>Formato con <Bold>negrita</Bold>, <Italic>cursiva</Italic> y <Underline>subrayado</Underline>.</Paragraph></ListItem>
                <ListItem><Paragraph>Crear materias con color e icono desde la barra lateral.</Paragraph></ListItem>
                <ListItem><Paragraph>Exportar cualquier nota a Word con el botón de arriba.</Paragraph></ListItem>
                <ListItem><Paragraph>Todo se guarda solo mientras escribes.</Paragraph></ListItem>
              </List>
              <Paragraph>Selecciona unas líneas y pulsa el botón de checklist para convertirlas
              en casillas; después haz clic en la casilla para marcarla.</Paragraph>
              <Paragraph>Atajos: Ctrl+N nota nueva · Ctrl+F buscar · Ctrl+B/I/U formato.</Paragraph>
            </Section>
            """;
        Db.AddNote(new Models.Note
        {
            Title = "Bienvenido a Notitas",
            ContentXaml = xaml,
            Preview = "Tu app local de apuntes. Algunas cosas que puedes hacer…",
        });
        var demo = Db.AddSubject(new Models.Subject { Name = "Matemáticas", ColorHex = "#3B82F6", Icon = "math" });
        Db.AddSubject(new Models.Subject { Name = "Historia", ColorHex = "#EF4444", Icon = "history" });
        Db.AddNote(new Models.Note
        {
            SubjectId = demo,
            Title = "Ejemplo: ecuaciones de segundo grado",
            ContentXaml = $"""
                <Section xmlns="{ns}">
                  <Paragraph FontSize="20" FontWeight="SemiBold">Ecuaciones de segundo grado</Paragraph>
                  <Paragraph>Tienen la forma <Italic>ax² + bx + c = 0</Italic>, donde <Italic>a</Italic> no es cero.</Paragraph>
                  <Paragraph FontWeight="SemiBold">Pasos para resolver</Paragraph>
                  <List MarkerStyle="Decimal">
                    <ListItem><Paragraph>Identificar los valores de a, b y c.</Paragraph></ListItem>
                    <ListItem><Paragraph>Calcular el discriminante.</Paragraph></ListItem>
                    <ListItem><Paragraph>Sustituir en la fórmula general.</Paragraph></ListItem>
                  </List>
                </Section>
                """,
            Preview = "Tienen la forma ax² + bx + c = 0, donde a no es cero.",
        });
        Log.Info("Contenido de bienvenida creado");
    }

    public static void ApplyTheme(string theme)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri($"Themes/{(theme == "Oscuro" ? "Oscuro" : "Claro")}.xaml", UriKind.Relative)
        };
        // sólo se reemplaza el diccionario de tema; el de iconos (índice 1) se conserva
        Current.Resources.MergedDictionaries[0] = dict;
        Log.Debug($"Tema aplicado: {theme}");
    }

    public static void ApplyAccent(string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            Current.Resources["AccentBrush"] = new SolidColorBrush(color);
            Current.Resources["AccentSoft"] = new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B));
            Log.Debug($"Color de tema aplicado: {hex}");
        }
        catch { Log.Warn($"Color inválido: {hex}"); }
    }
}
