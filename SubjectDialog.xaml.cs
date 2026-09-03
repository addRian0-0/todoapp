using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Notitas;

public partial class SubjectDialog : Window
{
    public string SubjectName => NameBox.Text;
    public string ColorHex { get; private set; }
    public string SubjectIcon { get; private set; }

    private static readonly string[] QuickColors =
        { "#8B5CF6", "#3B82F6", "#14B8A6", "#22C55E", "#EAB308", "#F97316", "#EF4444", "#EC4899" };

    public SubjectDialog(string name, string colorHex, string icon)
    {
        InitializeComponent();
        NameBox.Text = name;
        ColorHex = colorHex;
        SubjectIcon = icon;
        HexBox.Text = colorHex;
        if (name.Length > 0) OkButton.Content = "Guardar";

        Border? selectedIconBorder = null;
        foreach (var (key, label) in SubjectIcons.All)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 6, 6),
                BorderThickness = new Thickness(1),
            };
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            var glyph = new System.Windows.Shapes.Path
            {
                Style = (Style)FindResource("IconPath"),
                Data = (Geometry)FindResource(SubjectIcons.GeometryKey(key)),
            };
            var btn = new Button
            {
                Content = glyph,
                Width = 36, Height = 32,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = label,
            };
            border.Child = btn;
            if (key == icon)
            {
                border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                selectedIconBorder = border;
            }
            btn.Click += (_, _) =>
            {
                SubjectIcon = key;
                selectedIconBorder?.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                selectedIconBorder = border;
            };
            IconRow.Children.Add(border);
        }

        foreach (var hex in QuickColors)
        {
            var b = new Button
            {
                Width = 26, Height = 26, Margin = new Thickness(0, 0, 8, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Content = new Ellipse
                {
                    Width = 22, Height = 22,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))
                },
                Tag = hex
            };
            b.Click += (_, _) => { HexBox.Text = (string)b.Tag; };
            Swatches.Children.Add(b);
        }
        UpdatePreview();
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(HexBox.Text.Trim());
            PreviewBox.Background = new SolidColorBrush(c);
            ColorHex = HexBox.Text.Trim();
        }
        catch { /* hex incompleto mientras se escribe */ }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (NameBox.Text.Trim().Length == 0) { NameBox.Focus(); return; }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
