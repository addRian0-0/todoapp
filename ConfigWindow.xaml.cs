using Microsoft.Win32;
using Notitas.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Notitas;

public partial class ConfigWindow : Window
{
    private const string AppVersion = "0.4.0";

    private static readonly string[] QuickColors =
        { "#8B5CF6", "#EC4899", "#EF4444", "#F97316", "#EAB308", "#22C55E", "#14B8A6", "#3B82F6", "#6B7280" };

    private bool _ready;

    public ConfigWindow()
    {
        InitializeComponent();
        HexBox.Text = Settings.Current.AccentHex;
        ExportFolderText.Text = Settings.Current.ExportFolder;
        DbPathText.Text = $"Base de datos: {Db.DbPath}";
        try
        {
            var size = File.Exists(Db.DbPath) ? new FileInfo(Db.DbPath).Length : 0;
            DbSizeText.Text = $"Tamaño actual: {size / 1024.0:F1} KB";
        }
        catch { DbSizeText.Text = ""; }

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
            b.Click += (_, _) => HexBox.Text = (string)b.Tag;
            Swatches.Children.Add(b);
        }

        foreach (var lvl in new[] { "Debug", "Info", "Warn", "Error" })
            LogLevelBox.Items.Add(lvl);
        LogLevelBox.SelectedItem = Settings.Current.LogLevel;

        HighlightTheme();
        RefreshRuntime();
        RefreshEvents();
        Log.Changed += OnLogChanged;
        Closed += (_, _) => Log.Changed -= OnLogChanged;
        _ready = true;
    }

    private void OnLogChanged() => Dispatcher.BeginInvoke(RefreshEvents);

    private void HighlightTheme()
    {
        bool dark = Settings.Current.Theme == "Oscuro";
        LightBtn.Background = dark ? Brushes.Transparent : (Brush)FindResource("SelectedBg");
        DarkBtn.Background = dark ? (Brush)FindResource("SelectedBg") : Brushes.Transparent;
    }

    private void RefreshRuntime()
    {
        RuntimeInfo.Children.Clear();
        void Row(string k, string v)
        {
            var dp = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var key = new TextBlock { Text = k, Width = 170, Foreground = (Brush)FindResource("TextSecondary") };
            dp.Children.Add(key);
            dp.Children.Add(new TextBlock { Text = v, TextTrimming = TextTrimming.CharacterEllipsis });
            RuntimeInfo.Children.Add(dp);
        }
        var proc = Process.GetCurrentProcess();
        Row("Estado", "En ejecución");
        Row("Versión", AppVersion);
        Row("Plataforma", Environment.OSVersion.ToString());
        Row(".NET", Environment.Version.ToString());
        Row("Memoria", $"{proc.WorkingSet64 / (1024.0 * 1024.0):F1} MB");
        Row("Base de datos", Db.DbPath);
        Row("Carpeta de logs", Log.LogDir);
        Row("Autosave", "Activo (al escribir, 1.2 s)");
        Row("Sesión iniciada", proc.StartTime.ToString("HH:mm:ss"));
    }

    private void RefreshEvents()
    {
        EventsList.Items.Clear();
        foreach (var e in Log.Recent.AsEnumerable().Reverse().Take(60))
        {
            var dp = new DockPanel { Margin = new Thickness(4, 2, 4, 2) };
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 7, Height = 7,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = e.Level switch
                {
                    "Error" => Brushes.IndianRed,
                    "Warn" => Brushes.Goldenrod,
                    "Debug" => Brushes.SteelBlue,
                    _ => Brushes.MediumSeaGreen
                }
            };
            dp.Children.Add(dot);
            dp.Children.Add(new TextBlock
            {
                Text = $"{e.Time:HH:mm:ss}  {e.Level.ToUpper(),-5}  {e.Message}",
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            EventsList.Items.Add(dp);
        }
    }

    private void Light_Click(object s, RoutedEventArgs e) => SetTheme("Claro");
    private void Dark_Click(object s, RoutedEventArgs e) => SetTheme("Oscuro");

    private void SetTheme(string theme)
    {
        Settings.Current.Theme = theme;
        Settings.Save();
        App.ApplyTheme(theme);
        HighlightTheme();
    }

    private bool _syncingRgb;

    private void HexBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var c = (Color)ColorConverter.ConvertFromString(HexBox.Text.Trim());
            AccentPreview.Background = new SolidColorBrush(c);
            _syncingRgb = true;
            SlR.Value = c.R; SlG.Value = c.G; SlB.Value = c.B;
            ValR.Text = c.R.ToString(); ValG.Text = c.G.ToString(); ValB.Text = c.B.ToString();
            _syncingRgb = false;
            if (!_ready) return;
            Settings.Current.AccentHex = HexBox.Text.Trim();
            Settings.Save();
            App.ApplyAccent(Settings.Current.AccentHex);
        }
        catch { /* hex incompleto mientras se escribe */ }
    }

    private void Rgb_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncingRgb || SlR is null || SlG is null || SlB is null) return;
        HexBox.Text = $"#{(byte)SlR.Value:X2}{(byte)SlG.Value:X2}{(byte)SlB.Value:X2}";
    }

    private void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Carpeta de exportación por defecto" };
        if (Directory.Exists(Settings.Current.ExportFolder))
            dlg.InitialDirectory = Settings.Current.ExportFolder;
        if (dlg.ShowDialog(this) == true)
        {
            Settings.Current.ExportFolder = dlg.FolderName;
            Settings.Save();
            ExportFolderText.Text = dlg.FolderName;
            Log.Info($"Carpeta de exportación: {dlg.FolderName}");
        }
    }

    private void LogLevel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || LogLevelBox.SelectedItem is not string lvl) return;
        Settings.Current.LogLevel = lvl;
        Settings.Save();
        Log.MinLevel = lvl;
        Log.Info($"Nivel de log: {lvl}");
    }

    private void OpenDataDir_Click(object sender, RoutedEventArgs e) => OpenPath(Db.DataDir);
    private void OpenLogs_Click(object sender, RoutedEventArgs e) => OpenPath(Log.LogDir);

    private static void OpenPath(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error($"No se pudo abrir la carpeta: {ex.Message}"); }
    }

    private void CopyDiag_Click(object sender, RoutedEventArgs e)
    {
        var proc = Process.GetCurrentProcess();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Diagnóstico Notitas ===");
        sb.AppendLine($"Versión: {AppVersion}");
        sb.AppendLine($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"SO: {Environment.OSVersion}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Memoria: {proc.WorkingSet64 / (1024.0 * 1024.0):F1} MB");
        sb.AppendLine($"BD: {Db.DbPath}");
        sb.AppendLine($"Logs: {Log.LogDir}");
        sb.AppendLine("--- Eventos recientes ---");
        foreach (var ev in Log.Recent.TakeLast(40))
            sb.AppendLine($"{ev.Time:HH:mm:ss} [{ev.Level}] {ev.Message}");
        try
        {
            Clipboard.SetText(sb.ToString());
            Log.Info("Diagnóstico copiado al portapapeles");
        }
        catch (Exception ex) { Log.Error($"No se pudo copiar: {ex.Message}"); }
    }
}
