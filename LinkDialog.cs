using System.Windows;
using System.Windows.Controls;

namespace Notitas;

/// <summary>Diálogo mínimo para pedir la URL de un enlace.</summary>
public class LinkDialog : Window
{
    private readonly TextBox _urlBox;
    public string Url => _urlBox.Text;

    public LinkDialog()
    {
        Title = "Insertar enlace";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "CardBg");
        SetResourceReference(ForegroundProperty, "TextBrush");
        // cadena con reservas: si falta una fuente, WPF llama a FailFast y mata el proceso
        FontFamily = new System.Windows.Media.FontFamily("Segoe UI, Tahoma, Arial");
        FontSize = 13;

        var root = new StackPanel { Margin = new Thickness(18) };
        var label = new TextBlock { Text = "Dirección (URL)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
        _urlBox = new TextBox { Padding = new Thickness(8, 6, 8, 6), Text = "https://" };
        _urlBox.SetResourceReference(TextBox.BackgroundProperty, "PanelBg");
        _urlBox.SetResourceReference(TextBox.ForegroundProperty, "TextBrush");
        _urlBox.SetResourceReference(TextBox.CaretBrushProperty, "TextBrush");
        _urlBox.SetResourceReference(TextBox.BorderBrushProperty, "AccentBrush");
        _urlBox.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) DialogResult = true; };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Cancelar", Margin = new Thickness(0, 0, 8, 0) };
        cancel.SetResourceReference(StyleProperty, "FlatButton");
        cancel.Click += (_, _) => DialogResult = false;
        var ok = new Button { Content = "Insertar" };
        ok.SetResourceReference(StyleProperty, "AccentButton");
        ok.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        root.Children.Add(label);
        root.Children.Add(_urlBox);
        root.Children.Add(buttons);
        Content = root;

        Loaded += (_, _) => { _urlBox.Focus(); _urlBox.CaretIndex = _urlBox.Text.Length; };
    }
}
