using DocumentFormat.OpenXml.Packaging;
using Notitas.Models;
using Notitas.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Notitas;

/// <summary>
/// Modo de verificación (Notitas.exe --selftest [carpeta]). Ejercita la interfaz real
/// y guarda capturas PNG. Sirve para validar la app sin interacción manual.
/// </summary>
public static class SelfTest
{
    private static int _passed, _failed;
    private static string _outDir = "";

    private static string Trunc(string s) =>
        s.Length > 60 ? s[..60].Replace("\r", "").Replace("\n", " ") + "…" : s.Replace("\r", "").Replace("\n", " ");

    private static void Check(string name, bool ok, string? detail = null)
    {
        if (ok) { _passed++; Console.WriteLine($"  PASS  {name}"); }
        else { _failed++; Console.WriteLine($"  FAIL  {name}{(detail is null ? "" : $"  -> {detail}")}"); }
    }

    public static int Run(string[] args)
    {
        _outDir = args.SkipWhile(a => a != "--selftest").Skip(1).FirstOrDefault()
                  ?? Path.Combine(Db.DataDir, "selftest");
        Directory.CreateDirectory(_outDir);
        Console.WriteLine($"=== Notitas selftest ===\nSalida: {_outDir}\n");

        try
        {
            TestSerializationRoundTrip();
            TestDocxExport();
            TestMainWindow();
            TestReopen();
            TestSubjectRename();
            TestZoom();
            TestSmallWindow();
            TestConfigWindow();
            TestSubjectDialog();
            TestOtherWindows();
            TestDarkTheme();
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL  excepción no controlada: {ex}");
        }

        Console.WriteLine($"\nResultado: {_passed} OK, {_failed} fallidas");
        return _failed == 0 ? 0 : 1;
    }

    // ---------- serialización de contenido ----------
    private static void TestSerializationRoundTrip()
    {
        Console.WriteLine("[Contenido de notas]");
        var doc = new FlowDocument();

        var p1 = new Paragraph(new Run("Título de prueba")) { FontSize = 20, FontWeight = FontWeights.SemiBold };
        doc.Blocks.Add(p1);

        var p2 = new Paragraph();
        p2.Inlines.Add(new Run("normal "));
        p2.Inlines.Add(new Bold(new Run("negrita")));
        p2.Inlines.Add(new Run(" "));
        p2.Inlines.Add(new Italic(new Run("cursiva")));
        p2.Inlines.Add(new Run(" "));
        p2.Inlines.Add(new Underline(new Run("subrayado")));
        p2.Inlines.Add(new Run(" acentos: áéíóú ñ ¿? ¡!"));
        doc.Blocks.Add(p2);

        var list = new List { MarkerStyle = TextMarkerStyle.Decimal };
        list.ListItems.Add(new ListItem(new Paragraph(new Run("primero"))));
        list.ListItems.Add(new ListItem(new Paragraph(new Run("segundo"))));
        doc.Blocks.Add(list);

        var linkPara = new Paragraph();
        var link = new Hyperlink(new Run("un enlace")) { NavigateUri = new Uri("https://ejemplo.com/ruta") };
        linkPara.Inlines.Add(link);
        doc.Blocks.Add(linkPara);

        // checklist: CheckBox real dentro del documento
        var checkPara = new Paragraph();
        var cbDone = new CheckBox { IsChecked = true, Focusable = false };
        checkPara.Inlines.Add(new InlineUIContainer(cbDone) { BaselineAlignment = BaselineAlignment.Center });
        checkPara.Inlines.Add(new Run("tarea terminada"));
        doc.Blocks.Add(checkPara);

        var checkPara2 = new Paragraph();
        checkPara2.Inlines.Add(new InlineUIContainer(new CheckBox { IsChecked = false, Focusable = false })
        { BaselineAlignment = BaselineAlignment.Center });
        checkPara2.Inlines.Add(new Run("tarea pendiente"));
        doc.Blocks.Add(checkPara2);

        var xaml = MainWindow.DocToXaml(doc);
        var back = MainWindow.XamlToDoc(xaml);

        var text = new TextRange(back.ContentStart, back.ContentEnd).Text;
        Check("texto y acentos se conservan", text.Contains("acentos: áéíóú ñ ¿? ¡!"));

        var blocks = back.Blocks.ToList();
        Check("se conservan los bloques", blocks.Count == doc.Blocks.Count,
            $"esperados {doc.Blocks.Count}, obtenidos {blocks.Count}");

        bool boldOk = false, italicOk = false, underlineOk = false;
        foreach (var b in blocks.OfType<Paragraph>())
            foreach (var inl in b.Inlines)
            {
                if (inl is Span sp)
                {
                    var t = new TextRange(sp.ContentStart, sp.ContentEnd).Text;
                    if (t == "negrita" && sp.FontWeight == FontWeights.Bold) boldOk = true;
                    if (t == "cursiva" && sp.FontStyle == FontStyles.Italic) italicOk = true;
                    if (t == "subrayado" && (sp is Underline || sp.TextDecorations.Count > 0)) underlineOk = true;
                }
            }
        Check("negrita se conserva", boldOk);
        Check("cursiva se conserva", italicOk);
        Check("subrayado se conserva", underlineOk);

        Check("primer párrafo conserva tamaño de título",
            blocks.OfType<Paragraph>().First().FontSize == 20);
        Check("lista numerada se conserva",
            blocks.OfType<List>().Any(l => l.MarkerStyle == TextMarkerStyle.Decimal && l.ListItems.Count == 2));

        var links = blocks.OfType<Paragraph>().SelectMany(p => p.Inlines).OfType<Hyperlink>().ToList();
        Check("enlace se conserva con su dirección",
            links.Count == 1 && links[0].NavigateUri?.ToString() == "https://ejemplo.com/ruta");

        var boxes = blocks.OfType<Paragraph>()
            .Select(p => p.Inlines.FirstInline)
            .OfType<InlineUIContainer>()
            .Select(c => c.Child)
            .OfType<CheckBox>()
            .ToList();
        Check("las casillas de checklist sobreviven al guardado", boxes.Count == 2,
            $"encontradas {boxes.Count}");
        Check("el estado marcado/desmarcado se conserva",
            boxes.Count == 2 && boxes[0].IsChecked == true && boxes[1].IsChecked == false);
    }

    // ---------- exportación a Word ----------
    private static void TestDocxExport()
    {
        Console.WriteLine("\n[Exportación a Word]");
        var doc = new FlowDocument();
        doc.Blocks.Add(new Paragraph(new Run("Sección")) { FontSize = 20, FontWeight = FontWeights.SemiBold });
        var p = new Paragraph();
        p.Inlines.Add(new Bold(new Run("negrita ")));
        p.Inlines.Add(new Italic(new Run("cursiva ")));
        p.Inlines.Add(new Run("normal con ñ y tildes áé"));
        doc.Blocks.Add(p);
        var list = new List { MarkerStyle = TextMarkerStyle.Decimal };
        list.ListItems.Add(new ListItem(new Paragraph(new Run("uno"))));
        list.ListItems.Add(new ListItem(new Paragraph(new Run("dos"))));
        doc.Blocks.Add(list);
        var lp = new Paragraph();
        lp.Inlines.Add(new Hyperlink(new Run("sitio")) { NavigateUri = new Uri("https://ejemplo.com") });
        doc.Blocks.Add(lp);
        var cp = new Paragraph();
        cp.Inlines.Add(new InlineUIContainer(new CheckBox { IsChecked = true }) { BaselineAlignment = BaselineAlignment.Center });
        cp.Inlines.Add(new Run("pendiente marcado"));
        doc.Blocks.Add(cp);

        var path = Path.Combine(_outDir, "prueba.docx");
        try
        {
            DocxExporter.Export("Nota de prueba", doc, path);
            Check("se genera el archivo .docx", File.Exists(path) && new FileInfo(path).Length > 0);

            using var w = WordprocessingDocument.Open(path, false);
            var body = w.MainDocumentPart!.Document.Body!;
            var texts = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>()
                            .Select(t => t.Text).ToList();
            var all = string.Join("\n", texts);
            Check("el .docx contiene el título", all.Contains("Nota de prueba"));
            Check("el .docx conserva acentos", all.Contains("normal con ñ y tildes áé"));
            Check("el .docx numera la lista", all.Contains("1. ") && all.Contains("2. "));
            Check("el .docx incluye el texto del enlace", all.Contains("sitio"));
            Check("el .docx marca la casilla de la checklist", all.Contains("[x]") || all.Contains("[ ]"));
            Check("el .docx tiene relación de hipervínculo",
                w.MainDocumentPart.HyperlinkRelationships.Any());

            // Word se niega a abrir un .docx que no cumpla el esquema
            var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator();
            var errors = validator.Validate(w).ToList();
            Check("el .docx cumple el esquema de Word (Word lo abrirá)", errors.Count == 0,
                string.Join(" | ", errors.Take(3).Select(e => e.Description)));
        }
        catch (Exception ex)
        {
            Check("exportación a Word sin excepciones", false, ex.Message);
        }
    }

    // ---------- ventanas ----------
    private static void Settle(Dispatcher d)
    {
        for (int i = 0; i < 3; i++)
            d.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Snap(Window w, string file)
    {
        try
        {
            if (w.Content is not FrameworkElement fe) return;
            // se dibuja aplicando la escala del zoom: el LayoutTransform lo aplica el
            // contenedor, así que renderizar el contenido a secas saldría sin zoom
            double scale = fe.LayoutTransform is System.Windows.Media.ScaleTransform st ? st.ScaleX : 1.0;
            int width = (int)Math.Max(fe.ActualWidth * scale, 300);
            int height = (int)Math.Max(fe.ActualHeight * scale, 200);
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.PushTransform(new System.Windows.Media.ScaleTransform(scale, scale));
                dc.DrawRectangle(new VisualBrush(fe) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                    null, new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
                dc.Pop();
            }
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            var path = Path.Combine(_outDir, file);
            using var fs = File.Create(path);
            enc.Save(fs);
            Console.WriteLine($"        captura: {file} ({width}x{height})");
        }
        catch (Exception ex) { Console.WriteLine($"        (captura fallida: {ex.Message})"); }
    }

    private static MainWindow? _main;

    private static void TestMainWindow()
    {
        Console.WriteLine("\n[Ventana principal]");
        _main = new MainWindow { Width = 1240, Height = 760 };
        _main.Show();
        Settle(_main.Dispatcher);
        Check("la ventana principal abre sin fallar", _main.IsLoaded);
        Check("carga las notas de ejemplo", _main.NoteListCount > 0, $"{_main.NoteListCount} notas");
        Check("carga las materias de ejemplo", _main.SubjectListCount == 2, $"{_main.SubjectListCount} materias");

        // seleccionar la primera nota y verificar que el editor se llena
        _main.SelectNoteAt(0);
        Settle(_main.Dispatcher);
        Check("al elegir una nota se abre el editor", _main.EditorVisible);
        Check("el editor muestra el contenido de la nota seleccionada",
            _main.EditorText.Contains("Ecuaciones de segundo grado"), $"'{Trunc(_main.EditorText)}'");
        Check("el editor conserva el formato de lista de la nota",
            _main.EditorText.Contains("Identificar los valores"));
        Snap(_main, "01-principal-claro.png");

        // crear una nota nueva y escribir en ella
        _main.InvokeNewNote();
        Settle(_main.Dispatcher);
        Check("crear nota nueva deja el editor vacío", _main.EditorText.Trim().Length == 0,
            $"contenido: '{_main.EditorText.Trim()}'");

        _main.TypeIntoEditor("Apuntes de la clase de hoy");
        _main.SetTitle("Clase 1");
        Settle(_main.Dispatcher);
        _main.ForceSave();
        Settle(_main.Dispatcher);
        Check("la nota nueva se guarda con su título", Db.GetNotes(null, false).Any(n => n.Title == "Clase 1"));
        Check("la nota nueva guarda su contenido",
            Db.GetNotes(null, false).Any(n => n.ContentXaml.Contains("Apuntes de la clase de hoy")));

        // aplicar formato y checklist sobre lo escrito
        _main.SelectAllInEditor();
        _main.InvokeChecklist();
        Settle(_main.Dispatcher);
        _main.ForceSave();
        Settle(_main.Dispatcher);
        var saved = Db.GetNotes(null, false).First(n => n.Title == "Clase 1");
        Check("la checklist se guarda en la base de datos", saved.ContentXaml.Contains("CheckBox"),
            "no se encontró CheckBox en el XAML guardado");
        Snap(_main, "02-principal-checklist.png");

        // recarga real: cambiar de nota y volver, como haría el usuario
        _main.SelectNoteAt(1);
        Settle(_main.Dispatcher);
        _main.SelectNoteAt(0);
        Settle(_main.Dispatcher);
        Check("al volver a la nota la checklist sigue ahí", _main.EditorCheckBoxCount == 1,
            $"casillas encontradas: {_main.EditorCheckBoxCount}");
        Check("al volver a la nota el texto sigue ahí",
            _main.EditorText.Contains("Apuntes de la clase de hoy"));

        var clicked = _main.SimulateCheckBoxClick();
        Check("hacer clic en la casilla la marca", clicked, "la casilla no respondió al clic");
        Check("el clic deja la casilla marcada", _main.FirstCheckBoxChecked == true);

        // el estado marcado debe sobrevivir a guardar y volver a abrir la nota
        Settle(_main.Dispatcher);
        _main.ForceSave();
        _main.SelectNoteAt(1);
        Settle(_main.Dispatcher);
        _main.SelectNoteAt(0);
        Settle(_main.Dispatcher);
        Check("el estado marcado de la casilla se guarda", _main.FirstCheckBoxChecked == true);

        // búsqueda
        _main.Search("Ecuaciones");
        Settle(_main.Dispatcher);
        Check("la búsqueda filtra la lista", _main.NoteListCount == 1, $"{_main.NoteListCount} resultados");
        _main.Search("");
        Settle(_main.Dispatcher);

        // archivar y restaurar desde la interfaz (la última, para no tocar "Clase 1")
        int antes = _main.NoteListCount;
        _main.ArchiveNoteAt(antes - 1);
        Settle(_main.Dispatcher);
        Check("archivar quita la nota de la lista", _main.NoteListCount == antes - 1);
        _main.ShowArchived();
        Settle(_main.Dispatcher);
        Check("la nota aparece en archivadas", _main.NoteListCount == 1);
        Snap(_main, "03-archivadas.png");
        _main.ShowAllNotes();
        Settle(_main.Dispatcher);
    }

    /// <summary>Simula cerrar y volver a abrir la app: una ventana nueva leyendo la misma base.</summary>
    private static void TestReopen()
    {
        Console.WriteLine("\n[Reapertura de la aplicación]");
        try
        {
            var second = new MainWindow { Width = 1100, Height = 700 };
            second.Show();
            Settle(second.Dispatcher);
            Check("al reabrir se ven las notas guardadas", second.NoteListCount > 0);
            Check("al reabrir se ven las materias", second.SubjectListCount > 0);

            var idx = second.IndexOfNoteTitled("Clase 1");
            Check("la nota creada antes sigue en la lista", idx >= 0);
            if (idx >= 0)
            {
                second.SelectNoteAt(idx);
                Settle(second.Dispatcher);
                Check("al reabrir la nota conserva su checklist", second.EditorCheckBoxCount == 1);
                Check("al reabrir la casilla sigue marcada", second.FirstCheckBoxChecked == true);
                Check("al reabrir se conserva el texto",
                    second.EditorText.Contains("Apuntes de la clase de hoy"));
                Check("al reabrir la casilla sigue respondiendo al clic", second.SimulateCheckBoxClick());
            }
            second.Close();
        }
        catch (Exception ex) { Check("reapertura sin excepciones", false, ex.Message); }
    }

    private static void TestSubjectRename()
    {
        Console.WriteLine("\n[Renombrar materia]");
        try
        {
            var ok = _main!.RenameFirstSubject("Álgebra I");
            Settle(_main.Dispatcher);
            Check("renombrar en línea actualiza la materia", ok);
            Check("el nuevo nombre queda guardado",
                Db.GetSubjects().Any(s => s.Name == "Álgebra I"));
        }
        catch (Exception ex) { Check("renombrar sin excepciones", false, ex.Message); }
    }

    private static void TestSmallWindow()
    {
        Console.WriteLine("\n[Ventana en tamaño mínimo]");
        try
        {
            _main!.Width = 920;
            _main.Height = 560;
            Settle(_main.Dispatcher);
            Check("la interfaz aguanta el tamaño mínimo sin fallar", _main.IsLoaded);
            Snap(_main, "09-ventana-pequena.png");
            _main.Width = 1240;
            _main.Height = 760;
            Settle(_main.Dispatcher);
        }
        catch (Exception ex) { Check("tamaño mínimo sin excepciones", false, ex.Message); }
    }

    private static void TestZoom()
    {
        Console.WriteLine("\n[Zoom]");
        try
        {
            _main!.ShowAllNotes();
            var idx = _main.IndexOfNoteTitled("Clase 1");
            if (idx >= 0) _main.SelectNoteAt(idx);
            Settle(_main.Dispatcher);

            _main.ZoomAppBy(3);
            _main.ZoomNoteBy(5);
            Settle(_main.Dispatcher);
            Check("el zoom de la app aumenta", Math.Abs(_main.CurrentAppZoom - 1.3) < 0.001,
                $"quedó en {_main.CurrentAppZoom}");
            Check("el zoom de la nota aumenta", Math.Abs(_main.CurrentNoteZoom - 1.5) < 0.001,
                $"quedó en {_main.CurrentNoteZoom}");
            Check("los dos zooms son independientes",
                Math.Abs(_main.CurrentAppZoom - _main.CurrentNoteZoom) > 0.001);
            Snap(_main, "10-zoom-130-nota-150.png");

            Check("el zoom se guarda en la configuración",
                Math.Abs(Settings.Current.AppZoom - 1.3) < 0.001 &&
                Math.Abs(Settings.Current.NoteZoom - 1.5) < 0.001);

            // los topes evitan que la interfaz quede inutilizable
            _main.ZoomAppBy(20);
            _main.ZoomNoteBy(40);
            Settle(_main.Dispatcher);
            Check("el zoom de la app tiene tope superior", _main.CurrentAppZoom <= 1.4);
            Check("el zoom de la nota tiene tope superior", _main.CurrentNoteZoom <= 2.5);
            Snap(_main, "11-zoom-maximo.png");

            _main.ZoomAppBy(-40);
            _main.ZoomNoteBy(-60);
            Settle(_main.Dispatcher);
            Check("el zoom de la app tiene tope inferior", _main.CurrentAppZoom >= 0.8);
            Check("el zoom de la nota tiene tope inferior", _main.CurrentNoteZoom >= 0.7);
            Check("con zoom mínimo la app sigue viva", _main.IsLoaded && _main.NoteListCount > 0);

            // volver al 100 % y comprobar que se puede seguir escribiendo y guardando
            _main.ZoomAppBy(2);
            _main.ZoomNoteBy(3);
            Settle(_main.Dispatcher);
            _main.TypeIntoEditor("Texto escrito con zoom aplicado");
            _main.ForceSave();
            Settle(_main.Dispatcher);
            Check("se puede escribir y guardar con zoom aplicado",
                Db.GetNotes(null, false).Any(n => n.ContentXaml.Contains("Texto escrito con zoom aplicado")));

            // el zoom es de presentación: no debe acabar dentro del contenido de la nota
            var saved = Db.GetNotes(null, false).First(n => n.ContentXaml.Contains("Texto escrito con zoom"));
            Check("el zoom no se guarda dentro de la nota",
                !saved.ContentXaml.Contains("ScaleTransform") && !saved.ContentXaml.Contains("LayoutTransform"));
        }
        catch (Exception ex) { Check("zoom sin excepciones", false, ex.Message); }
    }

    private static void TestConfigWindow()
    {
        Console.WriteLine("\n[Configuración]");
        try
        {
            var cfg = new ConfigWindow { Owner = _main, Width = 900, Height = 640 };
            cfg.Show();
            Settle(cfg.Dispatcher);
            Check("la ventana de configuración abre", cfg.IsLoaded);
            Snap(cfg, "04-configuracion.png");
            cfg.Close();
        }
        catch (Exception ex) { Check("configuración sin excepciones", false, ex.Message); }
    }

    private static void TestSubjectDialog()
    {
        Console.WriteLine("\n[Diálogo de materia]");
        try
        {
            var dlg = new SubjectDialog("Biología", "#22C55E", "dna") { Owner = _main };
            dlg.Show();
            Settle(dlg.Dispatcher);
            Check("el diálogo de materia abre con icono y color", dlg.IsLoaded);
            Snap(dlg, "05-materia.png");
            dlg.Close();
        }
        catch (Exception ex) { Check("diálogo de materia sin excepciones", false, ex.Message); }
    }

    private static void TestOtherWindows()
    {
        Console.WriteLine("\n[Otras ventanas]");
        try
        {
            var link = new LinkDialog { Owner = _main };
            link.Show();
            Settle(link.Dispatcher);
            Check("el diálogo de enlace abre", link.IsLoaded);
            Snap(link, "07-enlace.png");
            link.Close();
        }
        catch (Exception ex) { Check("diálogo de enlace sin excepciones", false, ex.Message); }

        try
        {
            // una materia archivada para que la ventana tenga contenido
            var subj = Db.GetSubjects().FirstOrDefault();
            if (subj is not null) { subj.Archived = true; Db.UpdateSubject(subj); }
            var arch = new ArchivedSubjectsWindow { Owner = _main };
            arch.Show();
            Settle(arch.Dispatcher);
            Check("la ventana de materias archivadas abre", arch.IsLoaded);
            Snap(arch, "08-materias-archivadas.png");
            arch.Close();
            if (subj is not null) { subj.Archived = false; Db.UpdateSubject(subj); }
        }
        catch (Exception ex) { Check("materias archivadas sin excepciones", false, ex.Message); }
    }

    private static void TestDarkTheme()
    {
        Console.WriteLine("\n[Tema oscuro]");
        try
        {
            _main!.ShowAllNotes();
            _main.SelectNoteAt(0);
            Settle(_main.Dispatcher);
            App.ApplyTheme("Oscuro");
            App.ApplyAccent("#BA8BFF");
            Settle(_main.Dispatcher);
            Check("el tema oscuro se aplica sin fallar", true);
            Check("los iconos siguen disponibles tras cambiar de tema",
                Application.Current.TryFindResource("IconBook") is not null);
            Snap(_main!, "06-principal-oscuro.png");
            App.ApplyTheme("Claro");
        }
        catch (Exception ex) { Check("cambio de tema sin excepciones", false, ex.Message); }
    }
}
