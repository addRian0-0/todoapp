using Notitas.Models;
using Notitas.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Notitas;

/// <summary>Lista de materias archivadas con opciones de restaurar o eliminar.</summary>
public class ArchivedSubjectsWindow : Window
{
    private readonly StackPanel _list = new();

    public ArchivedSubjectsWindow()
    {
        Title = "Materias archivadas";
        Width = 400;
        Height = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "WindowBg");
        SetResourceReference(ForegroundProperty, "TextBrush");
        FontFamily = new FontFamily("Segoe UI, Tahoma, Arial");
        FontSize = 13;

        var scroll = new ScrollViewer { Content = _list, Margin = new Thickness(16), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Content = scroll;
        Refresh();
    }

    private void Refresh()
    {
        _list.Children.Clear();
        var subjects = Db.GetSubjects(archived: true);
        if (subjects.Count == 0)
        {
            var empty = new TextBlock { Text = "No hay materias archivadas.", Margin = new Thickness(4) };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            _list.Children.Add(empty);
            return;
        }
        foreach (var s in subjects)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

            var del = new Button { Content = "Eliminar", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(6, 0, 0, 0) };
            del.SetResourceReference(StyleProperty, "FlatButton");
            del.SetResourceReference(ForegroundProperty, "DangerBrush");
            del.Click += (_, _) =>
            {
                if (MessageBox.Show(this, $"¿Eliminar la materia \"{s.Name}\"?\nLas notas no se borran; quedan sin materia.",
                        "Notitas", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    Db.DeleteSubject(s.Id);
                    Log.Info($"Materia eliminada: {s.Name}");
                    Refresh();
                }
            };

            var restore = new Button { Content = "Restaurar", Padding = new Thickness(8, 3, 8, 3) };
            restore.SetResourceReference(StyleProperty, "FlatButton");
            restore.Click += (_, _) =>
            {
                s.Archived = false;
                Db.UpdateSubject(s);
                Log.Info($"Materia restaurada: {s.Name}");
                Refresh();
            };

            DockPanel.SetDock(del, Dock.Right);
            DockPanel.SetDock(restore, Dock.Right);
            row.Children.Add(del);
            row.Children.Add(restore);

            var dot = new Ellipse { Width = 10, Height = 10, Margin = new Thickness(2, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            try { dot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(s.ColorHex)); }
            catch { dot.Fill = Brushes.Gray; }
            row.Children.Add(dot);
            row.Children.Add(new TextBlock
            {
                Text = s.Icon.Length > 0 ? $"{s.Icon} {s.Name}" : s.Name,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            _list.Children.Add(row);
        }
    }
}
