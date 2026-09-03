using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using System.Windows;
using System.Windows.Documents;

namespace Notitas.Services;

public static class DocxExporter
{
    public static void Export(string title, FlowDocument doc, string path)
    {
        using var word = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = word.AddMainDocumentPart();
        main.Document = new W.Document();
        var body = main.Document.AppendChild(new W.Body());

        var titleRun = new W.Run(new W.Text(title));
        titleRun.PrependChild(new W.RunProperties(new W.Bold(), new W.FontSize { Val = "40" }));
        var titlePara = new W.Paragraph(titleRun);
        titlePara.PrependChild(new W.ParagraphProperties(new W.SpacingBetweenLines { After = "240" }));
        body.AppendChild(titlePara);

        foreach (var block in doc.Blocks.ToList())
            AppendBlock(main, body, block, 0, null);

        main.Document.Save();
        Log.Info($"Exportación a Word completada: {System.IO.Path.GetFileName(path)}");
    }

    private static void AppendBlock(MainDocumentPart main, W.Body body, Block block, int level, string? marker)
    {
        switch (block)
        {
            case Paragraph p:
                body.AppendChild(ConvertParagraph(main, p, level, marker));
                break;
            case List list:
                int i = list.StartIndex > 0 ? list.StartIndex : 1;
                foreach (var item in list.ListItems.ToList())
                {
                    string m = list.MarkerStyle == System.Windows.TextMarkerStyle.Decimal ? $"{i}. " : "•  ";
                    bool first = true;
                    foreach (var inner in item.Blocks.ToList())
                    {
                        AppendBlock(main, body, inner, level + 1, first ? m : null);
                        first = false;
                    }
                    i++;
                }
                break;
            case Section s:
                foreach (var inner in s.Blocks.ToList())
                    AppendBlock(main, body, inner, level, marker);
                break;
        }
    }

    private static W.Paragraph ConvertParagraph(MainDocumentPart main, Paragraph p, int level, string? marker)
    {
        var wp = new W.Paragraph();
        if (level > 0)
            wp.AppendChild(new W.ParagraphProperties(
                new W.Indentation { Left = (360 * level).ToString() }));

        if (marker is not null)
            wp.AppendChild(new W.Run(new W.Text(marker) { Space = SpaceProcessingModeValues.Preserve }));

        foreach (var inline in p.Inlines.ToList())
            AppendInline(main, wp, inline, false, false, false, p.FontSize);

        return wp;
    }

    private static void AppendInline(MainDocumentPart main, OpenXmlCompositeElement parent, Inline inline,
        bool bold, bool italic, bool underline, double baseSize)
    {
        switch (inline)
        {
            case Run run:
                parent.AppendChild(MakeRun(run, bold, italic, underline, baseSize, isLink: false));
                break;

            case Hyperlink link:
                var wLink = new W.Hyperlink { History = true };
                try
                {
                    if (link.NavigateUri is not null && link.NavigateUri.IsAbsoluteUri)
                        wLink.Id = main.AddHyperlinkRelationship(link.NavigateUri, true).Id;
                }
                catch (Exception ex) { Log.Warn($"Enlace no exportable: {ex.Message}"); }
                parent.AppendChild(wLink);
                foreach (var child in link.Inlines.ToList())
                    AppendInlineAsLink(main, wLink, child, bold, italic, baseSize);
                break;

            case Span span:
                bool sb = bold || span.FontWeight == FontWeights.Bold;
                bool si = italic || span.FontStyle == FontStyles.Italic;
                bool su = underline || span is Underline || HasUnderline(span.TextDecorations);
                foreach (var child in span.Inlines.ToList())
                    AppendInline(main, parent, child, sb, si, su, baseSize);
                break;

            case LineBreak:
                parent.AppendChild(new W.Run(new W.Break()));
                break;

            case InlineUIContainer { Child: System.Windows.Controls.CheckBox cb }:
                // las casillas de checklist se exportan como marca de texto
                parent.AppendChild(new W.Run(
                    new W.Text(cb.IsChecked == true ? "[x] " : "[ ] ")
                    { Space = SpaceProcessingModeValues.Preserve }));
                break;
        }
    }

    private static void AppendInlineAsLink(MainDocumentPart main, OpenXmlCompositeElement parent, Inline inline,
        bool bold, bool italic, double baseSize)
    {
        if (inline is Run run)
            parent.AppendChild(MakeRun(run, bold, italic, underline: true, baseSize, isLink: true));
        else if (inline is Span span)
            foreach (var child in span.Inlines.ToList())
                AppendInlineAsLink(main, parent, child,
                    bold || span.FontWeight == FontWeights.Bold,
                    italic || span.FontStyle == FontStyles.Italic, baseSize);
    }

    private static W.Run MakeRun(Run run, bool bold, bool italic, bool underline, double baseSize, bool isLink)
    {
        var r = new W.Run();
        var rp = new W.RunProperties();
        // el orden importa: OOXML exige la secuencia b, i, color, sz, u, o Word
        // considera el archivo dañado y se niega a abrirlo
        if (bold || run.FontWeight == FontWeights.Bold) rp.AppendChild(new W.Bold());
        if (italic || run.FontStyle == FontStyles.Italic) rp.AppendChild(new W.Italic());
        if (isLink) rp.AppendChild(new W.Color { Val = "2563EB" });
        double size = run.FontSize > 0 ? run.FontSize : baseSize;
        if (size > 0)
            rp.AppendChild(new W.FontSize { Val = ((int)Math.Round(size * 1.5)).ToString() }); // DIP -> medios puntos
        if (underline || HasUnderline(run.TextDecorations))
            rp.AppendChild(new W.Underline { Val = W.UnderlineValues.Single });
        if (rp.HasChildren) r.AppendChild(rp);
        r.AppendChild(new W.Text(run.Text) { Space = SpaceProcessingModeValues.Preserve });
        return r;
    }

    private static bool HasUnderline(TextDecorationCollection? decorations)
    {
        if (decorations is null) return false;
        foreach (var d in decorations)
            if (d.Location == TextDecorationLocation.Underline) return true;
        return false;
    }
}
