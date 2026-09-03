using Microsoft.Win32;
using Notitas.Models;
using Notitas.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Notitas;

public partial class MainWindow : Window
{
    private List<Subject> _subjects = new();
    private Note? _current;
    private long? _filterSubject;
    private bool _showArchived;
    private bool _loadingNote;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromSeconds(1.2) };

    public MainWindow()
    {
        InitializeComponent();
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveCurrent(); };
        Closing += (_, _) => SaveCurrent();
        Editor.AddHandler(Hyperlink.RequestNavigateEvent,
            new System.Windows.Navigation.RequestNavigateEventHandler((_, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                }
                catch (Exception ex) { Log.Error($"No se pudo abrir el enlace: {ex.Message}"); }
                e.Handled = true;
            }));
        RefreshSubjects();
        RefreshNotes();
        ApplyAppZoom(Settings.Current.AppZoom, save: false);
        ApplyNoteZoom(Settings.Current.NoteZoom, save: false);
    }

    // ================= Zoom =================
    private const double AppZoomMin = 0.8, AppZoomMax = 1.4;
    private const double NoteZoomMin = 0.7, NoteZoomMax = 2.5;
    private const double ZoomStep = 0.1;

    private static double Clamp(double v, double min, double max) => Math.Round(Math.Clamp(v, min, max), 2);

    private void ApplyAppZoom(double zoom, bool save = true)
    {
        zoom = Clamp(zoom, AppZoomMin, AppZoomMax);
        AppScale.ScaleX = AppScale.ScaleY = zoom;
        AppZoomLabel.Content = $"{zoom * 100:0} %";
        // la ventana no debe encogerse por debajo de lo que ocupa la interfaz escalada
        MinWidth = 860 * zoom;
        MinHeight = 520 * zoom;
        if (save)
        {
            Settings.Current.AppZoom = zoom;
            Settings.Save();
            Log.Debug($"Zoom de la aplicación: {zoom * 100:0} %");
        }
    }

    private void ApplyNoteZoom(double zoom, bool save = true)
    {
        zoom = Clamp(zoom, NoteZoomMin, NoteZoomMax);
        NoteScale.ScaleX = NoteScale.ScaleY = zoom;
        NoteZoomLabel.Content = $"{zoom * 100:0} %";
        if (save)
        {
            Settings.Current.NoteZoom = zoom;
            Settings.Save();
            Log.Debug($"Zoom de la nota: {zoom * 100:0} %");
        }
    }

    private void AppZoomIn_Click(object s, RoutedEventArgs e) => ApplyAppZoom(AppScale.ScaleX + ZoomStep);
    private void AppZoomOut_Click(object s, RoutedEventArgs e) => ApplyAppZoom(AppScale.ScaleX - ZoomStep);
    private void AppZoomReset_Click(object s, RoutedEventArgs e) => ApplyAppZoom(1.0);
    private void NoteZoomIn_Click(object s, RoutedEventArgs e) => ApplyNoteZoom(NoteScale.ScaleX + ZoomStep);
    private void NoteZoomOut_Click(object s, RoutedEventArgs e) => ApplyNoteZoom(NoteScale.ScaleX - ZoomStep);
    private void NoteZoomReset_Click(object s, RoutedEventArgs e) => ApplyNoteZoom(1.0);

    internal double CurrentAppZoom => AppScale.ScaleX;
    internal double CurrentNoteZoom => NoteScale.ScaleX;
    internal void ZoomAppBy(int steps) => ApplyAppZoom(AppScale.ScaleX + steps * ZoomStep);
    internal void ZoomNoteBy(int steps) => ApplyNoteZoom(NoteScale.ScaleX + steps * ZoomStep);

    /// <summary>Crea un icono vectorial a partir de una geometría del diccionario Icons.xaml.</summary>
    private System.Windows.Shapes.Path MakeIcon(string key, double size = 16, string style = "IconPath")
    {
        var p = new System.Windows.Shapes.Path
        {
            Style = (Style)FindResource(style),
            Data = (Geometry)FindResource(key),
            Width = size,
            Height = size,
        };
        return p;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var mods = System.Windows.Input.Keyboard.Modifiers;
        bool ctrl = (mods & System.Windows.Input.ModifierKeys.Control) != 0;
        if (!ctrl) return;
        bool shift = (mods & System.Windows.Input.ModifierKeys.Shift) != 0;

        switch (e.Key)
        {
            case System.Windows.Input.Key.F:
                SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; break;
            case System.Windows.Input.Key.N:
                NewNote_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case System.Windows.Input.Key.S:
                _saveTimer.Stop(); SaveCurrent(); e.Handled = true; break;

            // Ctrl: zoom de la nota · Ctrl+Shift: zoom de toda la app
            case System.Windows.Input.Key.OemPlus:
            case System.Windows.Input.Key.Add:
                if (shift) ApplyAppZoom(AppScale.ScaleX + ZoomStep);
                else ApplyNoteZoom(NoteScale.ScaleX + ZoomStep);
                e.Handled = true; break;
            case System.Windows.Input.Key.OemMinus:
            case System.Windows.Input.Key.Subtract:
                if (shift) ApplyAppZoom(AppScale.ScaleX - ZoomStep);
                else ApplyNoteZoom(NoteScale.ScaleX - ZoomStep);
                e.Handled = true; break;
            case System.Windows.Input.Key.D0:
            case System.Windows.Input.Key.NumPad0:
                if (shift) ApplyAppZoom(1.0); else ApplyNoteZoom(1.0);
                e.Handled = true; break;
        }
    }

    // ================= Subjects =================
    private void RefreshSubjects()
    {
        _subjects = Db.GetSubjects();
        SubjectList.Items.Clear();
        foreach (var s in _subjects)
            SubjectList.Items.Add(BuildSubjectItem(s));
    }

    private ListBoxItem BuildSubjectItem(Subject s)
    {
        var dot = new Ellipse { Width = 10, Height = 10, Margin = new Thickness(2, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        try { dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.ColorHex)); }
        catch { dot.Fill = Brushes.Gray; }

        var label = new TextBlock { Text = s.Name, VerticalAlignment = VerticalAlignment.Center };
        var editBox = new TextBox { Text = s.Name, Visibility = Visibility.Collapsed, MinWidth = 90, Padding = new Thickness(2, 0, 2, 0) };

        var more = new Button
        {
            Content = MakeIcon("IconMore", 14, "IconPathSecondary"),
            Style = (Style)FindResource("FlatButton"),
            Padding = new Thickness(6, 2, 6, 2),
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var panel = new DockPanel();
        DockPanel.SetDock(more, Dock.Right);
        panel.Children.Add(more);
        panel.Children.Add(dot);
        if (SubjectIcons.IsKnown(s.Icon))
        {
            var ic = MakeIcon(SubjectIcons.GeometryKey(s.Icon), 15);
            ic.Margin = new Thickness(0, 0, 7, 0);
            ic.VerticalAlignment = VerticalAlignment.Center;
            panel.Children.Add(ic);
        }
        panel.Children.Add(label);
        panel.Children.Add(editBox);

        var item = new ListBoxItem { Content = panel, Tag = s };

        void CommitRename()
        {
            var name = editBox.Text.Trim();
            if (name.Length > 0 && name != s.Name)
            {
                s.Name = name;
                Db.UpdateSubject(s);
                Log.Info($"Materia renombrada: {name}");
            }
            label.Text = s.Name;
            editBox.Visibility = Visibility.Collapsed;
            label.Visibility = Visibility.Visible;
        }
        editBox.LostFocus += (_, _) => CommitRename();
        editBox.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) CommitRename(); };

        var menu = new ContextMenu();
        var rename = new MenuItem { Header = "Renombrar" };
        rename.Click += (_, _) =>
        {
            editBox.Text = s.Name;
            label.Visibility = Visibility.Collapsed;
            editBox.Visibility = Visibility.Visible;
            editBox.Focus();
            editBox.SelectAll();
        };
        var recolor = new MenuItem { Header = "Cambiar color o icono" };
        recolor.Click += (_, _) =>
        {
            var dlg = new SubjectDialog(s.Name, s.ColorHex, s.Icon) { Owner = this, Title = "Editar materia" };
            if (dlg.ShowDialog() == true)
            {
                s.Name = dlg.SubjectName; s.ColorHex = dlg.ColorHex; s.Icon = dlg.SubjectIcon;
                Db.UpdateSubject(s);
                RefreshSubjects();
            }
        };
        var archive = new MenuItem { Header = "Archivar" };
        archive.Click += (_, _) =>
        {
            s.Archived = true;
            Db.UpdateSubject(s);
            Log.Info($"Materia archivada: {s.Name}");
            RefreshSubjects(); RefreshNotes();
        };
        var delete = new MenuItem { Header = "Eliminar" };
        delete.SetResourceReference(MenuItem.ForegroundProperty, "DangerBrush");
        delete.Click += (_, _) =>
        {
            if (Confirm($"¿Eliminar la materia \"{s.Name}\"?", "Las notas no se borran; quedan sin materia."))
            {
                Db.DeleteSubject(s.Id);
                Log.Info($"Materia eliminada: {s.Name}");
                if (_filterSubject == s.Id) _filterSubject = null;
                RefreshSubjects(); RefreshNotes();
            }
        };
        menu.Items.Add(rename); menu.Items.Add(recolor); menu.Items.Add(archive);
        menu.Items.Add(new Separator()); menu.Items.Add(delete);
        more.Click += (_, _) => { menu.PlacementTarget = more; menu.IsOpen = true; };
        item.ContextMenu = menu;
        return item;
    }

    private void AddSubject_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SubjectDialog("", "#8B5CF6", "") { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SubjectName.Trim().Length > 0)
        {
            Db.AddSubject(new Subject { Name = dlg.SubjectName.Trim(), ColorHex = dlg.ColorHex, Icon = dlg.SubjectIcon });
            Log.Info($"Materia creada: {dlg.SubjectName}");
            RefreshSubjects();
        }
    }

    private void SubjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SubjectList.SelectedItem is ListBoxItem { Tag: Subject s })
        {
            _filterSubject = s.Id; _showArchived = false;
            ListHeader.Text = s.Name;
            RefreshNotes();
        }
    }

    // ================= Notes list =================
    private void RefreshNotes()
    {
        SaveCurrent();
        var notes = Db.GetNotes(_filterSubject, _showArchived, SearchBox.Text);
        NoteList.Items.Clear();
        foreach (var n in notes)
            NoteList.Items.Add(BuildNoteItem(n));
        ListCount.Text = notes.Count switch { 0 => "", 1 => "1 nota", var c => $"{c} notas" };
        EmptyListHint.Visibility = notes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyListHint.Text = SearchBox.Text.Length > 0
            ? "No se encontraron notas con esa búsqueda."
            : _showArchived ? "No hay notas archivadas." : "No hay notas aquí todavía.";
        if (_current is not null)
        {
            var match = NoteList.Items.Cast<ListBoxItem>().FirstOrDefault(i => ((Note)i.Tag).Id == _current.Id);
            if (match is not null) { _loadingNote = true; match.IsSelected = true; _loadingNote = false; }
            else ShowEditor(null);
        }
    }

    private ListBoxItem BuildNoteItem(Note n)
    {
        var stack = new StackPanel();
        var titleRow = new DockPanel();
        var moreBtn = new Button
        {
            Content = MakeIcon("IconMore", 14, "IconPathSecondary"),
            Style = (Style)FindResource("FlatButton"),
            Padding = new Thickness(6, 2, 6, 2),
            ToolTip = "Acciones de la nota",
        };
        DockPanel.SetDock(moreBtn, Dock.Right);
        titleRow.Children.Add(moreBtn);
        titleRow.Children.Add(new TextBlock { Text = n.Title, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        stack.Children.Add(titleRow);
        if (n.Preview.Length > 0)
        {
            var preview = new TextBlock
            {
                Text = n.Preview,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0),
            };
            // SetResourceReference y no FindResource: así el color sigue al cambiar de tema
            preview.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            stack.Children.Add(preview);
        }
        var meta = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        var subj = _subjects.FirstOrDefault(s => s.Id == n.SubjectId);
        if (subj is not null)
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 2),
                Child = new TextBlock { Text = subj.Name, FontSize = 11 },
            };
            chip.SetResourceReference(Border.BackgroundProperty, "SelectedBg");
            meta.Children.Add(chip);
        }
        var date = new TextBlock
        {
            Text = FormatDate(n.UpdatedAt),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        date.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
        DockPanel.SetDock(date, Dock.Right);
        meta.Children.Insert(0, date);
        stack.Children.Add(meta);

        var item = new ListBoxItem { Content = stack, Tag = n };
        var menu = new ContextMenu();

        var archive = new MenuItem { Header = n.Archived ? "Restaurar" : "Archivar nota" };
        archive.Click += (_, _) =>
        {
            n.Archived = !n.Archived;
            Db.UpdateNote(n);
            Log.Info(n.Archived ? $"Nota archivada: {n.Title}" : $"Nota restaurada: {n.Title}");
            if (_current?.Id == n.Id) { _current = null; ShowEditor(null); }
            RefreshNotes();
        };
        var duplicate = new MenuItem { Header = "Duplicar" };
        duplicate.Click += (_, _) =>
        {
            Db.AddNote(new Note { SubjectId = n.SubjectId, Title = n.Title + " (copia)", ContentXaml = n.ContentXaml, Preview = n.Preview });
            Log.Info($"Nota duplicada: {n.Title}");
            RefreshNotes();
        };
        var export = new MenuItem { Header = "Exportar Word" };
        export.Click += (_, _) => ExportNote(n);
        var delete = new MenuItem { Header = "Eliminar" };
        delete.SetResourceReference(MenuItem.ForegroundProperty, "DangerBrush");
        delete.Click += (_, _) =>
        {
            if (Confirm("¿Eliminar nota?", "Esta acción no se puede deshacer."))
            {
                Db.DeleteNote(n.Id);
                Log.Info($"Nota eliminada: {n.Title}");
                if (_current?.Id == n.Id) { _current = null; ShowEditor(null); }
                RefreshNotes();
            }
        };
        menu.Items.Add(archive); menu.Items.Add(duplicate); menu.Items.Add(export);
        menu.Items.Add(new Separator()); menu.Items.Add(delete);
        item.ContextMenu = menu;
        moreBtn.Click += (_, _) => { menu.PlacementTarget = moreBtn; menu.IsOpen = true; };
        return item;
    }

    private static string FormatDate(DateTime d)
    {
        if (d.Date == DateTime.Today) return $"Hoy, {d:HH:mm}";
        if (d.Date == DateTime.Today.AddDays(-1)) return $"Ayer, {d:HH:mm}";
        return d.ToString("dd/MM/yyyy");
    }

    private void NoteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingNote) return;
        if (NoteList.SelectedItem is ListBoxItem { Tag: Note n })
        {
            SaveCurrent();
            ShowEditor(n);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshNotes();
    }

    private void ArchivedSubjects_Click(object sender, RoutedEventArgs e)
    {
        new ArchivedSubjectsWindow { Owner = this }.ShowDialog();
        RefreshSubjects();
        RefreshNotes();
    }

    private void NavNotas_Click(object sender, RoutedEventArgs e)
    {
        _filterSubject = null; _showArchived = false;
        SubjectList.SelectedItem = null;
        ListHeader.Text = "Todas las notas";
        RefreshNotes();
    }

    private void NavArchivadas_Click(object sender, RoutedEventArgs e)
    {
        _filterSubject = null; _showArchived = true;
        SubjectList.SelectedItem = null;
        ListHeader.Text = "Archivadas";
        RefreshNotes();
    }

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrent();
        var n = new Note { SubjectId = _filterSubject, Title = "Nota sin título" };
        n.Id = Db.AddNote(n);
        Log.Info("Nota creada");
        _showArchived = false;
        // cargar el editor con la nota nueva ANTES de refrescar: RefreshNotes vuelve a
        // guardar y, si el editor aún mostrara la nota anterior, copiaría su contenido aquí
        ShowEditor(n);
        RefreshNotes();
        TitleBox.Focus();
        TitleBox.SelectAll();
    }

    // ================= Editor =================
    private void ShowEditor(Note? n)
    {
        _saveTimer.Stop();
        _loadingNote = true;
        _current = n;
        if (n is null)
        {
            EditorPane.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Visible;
            _loadingNote = false;
            return;
        }
        EditorPane.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
        TitleBox.Text = n.Title;
        Editor.Document = XamlToDoc(n.ContentXaml);
        WireCheckBoxes(Editor.Document);
        SaveState.Text = "Guardado";
        SaveIcon.Data = (Geometry)FindResource("IconCheck");
        UpdateMeta(n);

        NoteSubjectBox.Items.Clear();
        NoteSubjectBox.Items.Add(new ComboBoxItem { Content = "Sin materia", Tag = (long?)null });
        foreach (var s in _subjects)
            NoteSubjectBox.Items.Add(new ComboBoxItem { Content = s.Name, Tag = (long?)s.Id });
        NoteSubjectBox.SelectedIndex = Math.Max(0, _subjects.FindIndex(s => s.Id == n.SubjectId) + 1);
        _loadingNote = false;
    }

    private void Editor_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loadingNote || _current is null) return;
        SaveState.Text = "Guardando…";
        SaveIcon.Data = (Geometry)FindResource("IconRefresh");
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void NoteSubjectBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingNote || _current is null) return;
        if (NoteSubjectBox.SelectedItem is ComboBoxItem { Tag: var tag })
        {
            _current.SubjectId = tag as long?;
            Db.UpdateNote(_current);
            RefreshNotes();
        }
    }

    private void SaveCurrent()
    {
        if (_current is null || EditorPane.Visibility != Visibility.Visible) return;
        var text = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;
        // la vista previa va en una sola línea: se colapsan saltos y espacios repetidos
        var flat = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        _current.Title = TitleBox.Text.Trim().Length > 0 ? TitleBox.Text.Trim() : "Nota sin título";
        _current.ContentXaml = DocToXaml(Editor.Document);
        _current.Preview = flat.Length > 110 ? flat[..110] + "…" : flat;
        Db.UpdateNote(_current);
        SaveState.Text = "Guardado";
        SaveIcon.Data = (Geometry)FindResource("IconCheck");
        UpdateMeta(_current);
        Log.Debug($"Guardado automático completado: {_current.Title}");

        if (NoteList.SelectedItem is ListBoxItem { Tag: Note sel } item && sel.Id == _current.Id)
        {
            _loadingNote = true;
            var idx = NoteList.Items.IndexOf(item);
            NoteList.Items[idx] = BuildNoteItem(_current);
            ((ListBoxItem)NoteList.Items[idx]).IsSelected = true;
            _loadingNote = false;
        }
    }

    /// <summary>
    /// Serializa el documento con XamlWriter y no con TextRange.Save: este último
    /// descarta los InlineUIContainer, con lo que se perderían las casillas de checklist.
    /// </summary>
    internal static string DocToXaml(FlowDocument doc)
    {
        try
        {
            return System.Windows.Markup.XamlWriter.Save(doc);
        }
        catch (Exception ex)
        {
            Log.Error($"No se pudo serializar la nota, se guarda sólo el texto: {ex.Message}");
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            using var ms = new MemoryStream();
            range.Save(ms, DataFormats.Xaml);
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    internal static FlowDocument XamlToDoc(string xaml)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return new FlowDocument();
        try
        {
            var parsed = System.Windows.Markup.XamlReader.Parse(xaml);
            switch (parsed)
            {
                case FlowDocument fd:
                    return fd;
                case Section section:
                    // formato de las notas de ejemplo: un <Section> suelto
                    var wrapper = new FlowDocument();
                    var blocks = section.Blocks.ToList();
                    foreach (var b in blocks) { section.Blocks.Remove(b); wrapper.Blocks.Add(b); }
                    return wrapper;
                case Block block:
                    var single = new FlowDocument();
                    single.Blocks.Add(block);
                    return single;
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"Contenido en formato antiguo, se carga como texto enriquecido: {ex.Message}");
        }

        // respaldo: contenido guardado por versiones anteriores
        var doc = new FlowDocument();
        try
        {
            var range = new TextRange(doc.ContentStart, doc.ContentEnd);
            using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xaml));
            range.Load(ms, DataFormats.Xaml);
        }
        catch (Exception ex) { Log.Error($"No se pudo cargar el contenido de la nota: {ex.Message}"); }
        return doc;
    }

    private void UpdateMeta(Note n)
    {
        var text = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;
        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        MetaInfo.Text = $"{words} palabras   ·   Creada: {n.CreatedAt:dd/MM/yyyy}   ·   Última edición: {FormatDate(n.UpdatedAt)}";
    }

    // ---- formatting ----
    private void Bold_Click(object s, RoutedEventArgs e) { System.Windows.Documents.EditingCommands.ToggleBold.Execute(null, Editor); Editor.Focus(); }
    private void Italic_Click(object s, RoutedEventArgs e) { System.Windows.Documents.EditingCommands.ToggleItalic.Execute(null, Editor); Editor.Focus(); }
    private void Underline_Click(object s, RoutedEventArgs e) { System.Windows.Documents.EditingCommands.ToggleUnderline.Execute(null, Editor); Editor.Focus(); }
    private void Bullets_Click(object s, RoutedEventArgs e) { System.Windows.Documents.EditingCommands.ToggleBullets.Execute(null, Editor); Editor.Focus(); }
    private void Numbers_Click(object s, RoutedEventArgs e) { System.Windows.Documents.EditingCommands.ToggleNumbering.Execute(null, Editor); Editor.Focus(); }

    private void Heading_Click(object s, RoutedEventArgs e)
    {
        var sel = Editor.Selection;
        bool isHeading = sel.GetPropertyValue(TextElement.FontSizeProperty) is double d && d >= 19;
        sel.ApplyPropertyValue(TextElement.FontSizeProperty, isHeading ? 14.0 : 20.0);
        sel.ApplyPropertyValue(TextElement.FontWeightProperty, isHeading ? FontWeights.Normal : FontWeights.SemiBold);
        Editor.Focus();
    }

    // ---- checklist ----
    // Se usa un CheckBox real dentro de un InlineUIContainer en vez de los caracteres
    // ☐/☑: no depende de que la fuente tenga esos glifos y se puede marcar con un clic.
    private static CheckBox? ParagraphCheckBox(Paragraph p) =>
        p.Inlines.FirstInline is InlineUIContainer { Child: CheckBox cb } ? cb : null;

    private InlineUIContainer MakeCheckBox(bool isChecked)
    {
        var cb = new CheckBox
        {
            IsChecked = isChecked,
            Focusable = false,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        WireCheckBox(cb);
        return new InlineUIContainer(cb) { BaselineAlignment = BaselineAlignment.Center };
    }

    /// <summary>
    /// El manejador va en la casilla y no en el editor: los controles incrustados en un
    /// documento viven en una "isla" visual y sus eventos no llegan al RichTextBox.
    /// </summary>
    private void WireCheckBox(CheckBox cb)
    {
        cb.Focusable = false;
        cb.PreviewMouseLeftButtonDown -= CheckBox_MouseDown;
        cb.PreviewMouseLeftButtonDown += CheckBox_MouseDown;
    }

    private void CheckBox_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not CheckBox cb) return;
        cb.IsChecked = cb.IsChecked != true;
        e.Handled = true; // evita que ToggleButton lo vuelva a alternar
        Editor_Changed(this, null!);
    }

    private void WireCheckBoxes(FlowDocument doc)
    {
        foreach (var cb in CheckBoxesIn(doc)) WireCheckBox(cb);
    }

    private static List<CheckBox> CheckBoxesIn(FlowDocument doc)
    {
        var found = new List<CheckBox>();
        void Walk(BlockCollection blocks)
        {
            foreach (var b in blocks)
            {
                switch (b)
                {
                    case Paragraph p:
                        foreach (var inl in p.Inlines)
                            if (inl is InlineUIContainer { Child: CheckBox cb }) found.Add(cb);
                        break;
                    case List l:
                        foreach (var item in l.ListItems) Walk(item.Blocks);
                        break;
                    case Section sec:
                        Walk(sec.Blocks);
                        break;
                }
            }
        }
        Walk(doc.Blocks);
        return found;
    }

    private List<Paragraph> SelectedParagraphs()
    {
        var sel = Editor.Selection;
        var result = new List<Paragraph>();
        void Collect(BlockCollection blocks)
        {
            foreach (var b in blocks)
            {
                switch (b)
                {
                    case Paragraph p:
                        if (p.ContentEnd.CompareTo(sel.Start) >= 0 && p.ContentStart.CompareTo(sel.End) <= 0)
                            result.Add(p);
                        break;
                    case List list:
                        foreach (var item in list.ListItems) Collect(item.Blocks);
                        break;
                    case Section sec:
                        Collect(sec.Blocks);
                        break;
                }
            }
        }
        Collect(Editor.Document.Blocks);
        return result;
    }

    private void Checklist_Click(object s, RoutedEventArgs e)
    {
        var paragraphs = SelectedParagraphs();
        if (paragraphs.Count == 0) return;
        // si todas ya son checklist, se quitan; si no, se convierten todas
        bool allChecklist = paragraphs.All(p => ParagraphCheckBox(p) is not null);
        foreach (var p in paragraphs)
        {
            var existing = ParagraphCheckBox(p);
            if (allChecklist)
            {
                if (existing is not null) p.Inlines.Remove(p.Inlines.FirstInline);
            }
            else if (existing is null)
            {
                var box = MakeCheckBox(false);
                if (p.Inlines.FirstInline is null) p.Inlines.Add(box);
                else p.Inlines.InsertBefore(p.Inlines.FirstInline, box);
            }
        }
        Editor.Focus();
        Editor_Changed(this, null!);
    }

    /// <summary>Permite marcar las casillas con un clic dentro del editor.</summary>

    // ---- links ----
    private void Link_Click(object s, RoutedEventArgs e)
    {
        if (Editor.Selection.IsEmpty)
        {
            MessageBox.Show(this, "Primero selecciona el texto que será el enlace.", "Notitas",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new LinkDialog { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var url = dlg.Url.Trim();
        if (url.Length == 0) return;
        if (!url.Contains("://")) url = "https://" + url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            MessageBox.Show(this, "La dirección no es válida.", "Notitas", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            _ = new Hyperlink(Editor.Selection.Start, Editor.Selection.End) { NavigateUri = uri };
            Log.Info($"Enlace insertado: {uri}");
        }
        catch (Exception ex) { Log.Error($"No se pudo insertar el enlace: {ex.Message}"); }
        Editor.Focus();
    }

    // ================= Export =================
    private void ExportDocx_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrent();
        if (_current is not null) ExportNote(_current);
    }

    private void ExportNote(Note n)
    {
        var safe = string.Join("_", n.Title.Split(System.IO.Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var dlg = new SaveFileDialog
        {
            Title = "Exportar a Word",
            FileName = safe.Length > 0 ? safe : "Nota",
            DefaultExt = ".docx",
            Filter = "Documento de Word (*.docx)|*.docx",
            InitialDirectory = Directory.Exists(Settings.Current.ExportFolder)
                ? Settings.Current.ExportFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            var start = DateTime.Now;
            DocxExporter.Export(n.Title, XamlToDoc(n.ContentXaml), dlg.FileName);
            var ms = (DateTime.Now - start).TotalMilliseconds;
            if (ms > 800) Log.Warn($"Tiempo de exportación alto: {ms:F0} ms");
            Settings.Current.ExportFolder = System.IO.Path.GetDirectoryName(dlg.FileName) ?? Settings.Current.ExportFolder;
            Settings.Save();
        }
        catch (Exception ex)
        {
            Log.Error($"Error al exportar: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Error al exportar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ================= Misc =================
    private void Config_Click(object sender, RoutedEventArgs e)
    {
        new ConfigWindow { Owner = this }.ShowDialog();
    }

    private bool Confirm(string title, string detail) =>
        MessageBox.Show(this, $"{title}\n{detail}", "Notitas",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    // ================= Ganchos para --selftest =================
    // Permiten ejercitar la interfaz real sin ratón; no se usan en el uso normal.
    internal int NoteListCount => NoteList.Items.Count;
    internal int SubjectListCount => SubjectList.Items.Count;
    internal bool EditorVisible => EditorPane.Visibility == Visibility.Visible;
    internal string EditorText => new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;

    internal void SelectNoteAt(int index)
    {
        if (index < 0 || index >= NoteList.Items.Count) return;
        ((ListBoxItem)NoteList.Items[index]).IsSelected = true;
        NoteList_SelectionChanged(NoteList, null!);
    }

    internal int IndexOfNoteTitled(string title)
    {
        for (int i = 0; i < NoteList.Items.Count; i++)
            if (((Note)((ListBoxItem)NoteList.Items[i]).Tag).Title == title) return i;
        return -1;
    }

    internal bool RenameFirstSubject(string newName)
    {
        if (SubjectList.Items.Count == 0) return false;
        var item = (ListBoxItem)SubjectList.Items[0];
        var subject = (Subject)item.Tag;
        // misma ruta que el renombrado en línea: cambia el modelo y persiste
        subject.Name = newName;
        Db.UpdateSubject(subject);
        RefreshSubjects();
        return Db.GetSubjects().Any(s => s.Id == subject.Id && s.Name == newName);
    }

    internal void InvokeNewNote() => NewNote_Click(this, new RoutedEventArgs());
    internal void InvokeChecklist() => Checklist_Click(this, new RoutedEventArgs());
    internal void SetTitle(string title) => TitleBox.Text = title;
    internal void SelectAllInEditor() => Editor.SelectAll();
    internal void Search(string text) => SearchBox.Text = text;
    internal void ShowArchived() => NavArchivadas_Click(this, new RoutedEventArgs());
    internal void ShowAllNotes() => NavNotas_Click(this, new RoutedEventArgs());

    internal int EditorCheckBoxCount => EditorCheckBoxes().Count;
    internal bool? FirstCheckBoxChecked => EditorCheckBoxes().FirstOrDefault()?.IsChecked;

    private List<CheckBox> EditorCheckBoxes() => CheckBoxesIn(Editor.Document);

    /// <summary>
    /// Lanza sobre la casilla el mismo evento que produce un clic del ratón, para
    /// comprobar la ruta real de clic y no sólo la lógica interna.
    /// </summary>
    internal bool SimulateCheckBoxClick()
    {
        var cb = EditorCheckBoxes().FirstOrDefault();
        if (cb is null) return false;
        var before = cb.IsChecked;
        cb.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = cb,
        });
        return cb.IsChecked != before;
    }

    internal void ToggleFirstCheckBox()
    {
        var cb = EditorCheckBoxes().FirstOrDefault();
        if (cb is null) return;
        cb.IsChecked = cb.IsChecked != true;
        Editor_Changed(this, null!);
    }

    internal void TypeIntoEditor(string text)
    {
        Editor.Document.Blocks.Clear();
        Editor.Document.Blocks.Add(new Paragraph(new Run(text)));
        Editor_Changed(this, null!);
    }

    internal void ForceSave()
    {
        _saveTimer.Stop();
        SaveCurrent();
    }

    internal void ArchiveNoteAt(int index)
    {
        if (index < 0 || index >= NoteList.Items.Count) return;
        var note = (Note)((ListBoxItem)NoteList.Items[index]).Tag;
        note.Archived = true;
        Db.UpdateNote(note);
        if (_current?.Id == note.Id) { _current = null; ShowEditor(null); }
        RefreshNotes();
    }
}
