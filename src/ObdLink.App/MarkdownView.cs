using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ObdLink.Core.Docs;

namespace ObdLink.App;

/// <summary>Renders docs/GUIDE.md as a themed FlowDocument (headings, lists, tables) instead of raw text.</summary>
public static class MarkdownView
{
    public static FlowDocument Build(string md)
    {
        var doc = new FlowDocument { PagePadding = new Thickness(8), FontFamily = new FontFamily("Segoe UI"), FontSize = 13 };
        doc.SetResourceReference(FlowDocument.ForegroundProperty, "Fg");
        doc.SetResourceReference(FlowDocument.BackgroundProperty, "Field");
        List? list = null;
        foreach (var b in MarkdownBlocks.Parse(md))
        {
            if (b.Kind is not (MdKind.Bullet or MdKind.Numbered)) list = null;
            switch (b.Kind)
            {
                case MdKind.Heading:
                    var h = Para(b.Text); h.FontWeight = FontWeights.SemiBold; h.FontSize = b.Level switch { 1 => 22, 2 => 18, 3 => 15, _ => 14 };
                    h.Margin = new Thickness(0, b.Level <= 2 ? 14 : 8, 0, 4); doc.Blocks.Add(h); break;
                case MdKind.Paragraph: doc.Blocks.Add(Para(b.Text)); break;
                case MdKind.Bullet or MdKind.Numbered:
                    var marker = b.Kind == MdKind.Bullet ? TextMarkerStyle.Disc : TextMarkerStyle.Decimal;
                    if (list == null || list.MarkerStyle != marker)
                    {
                        list = new List { MarkerStyle = marker, Margin = new Thickness(0, 2, 0, 6) };
                        if (b.Kind == MdKind.Numbered) list.StartIndex = Math.Max(1, b.Level);
                        doc.Blocks.Add(list);
                    }
                    var p = Para(b.Text); p.Margin = new Thickness(0, 1, 0, 1); list.ListItems.Add(new ListItem(p)); break;
                case MdKind.Code:
                    var c = new Paragraph(new Run(b.Text)) { FontFamily = new FontFamily("Cascadia Mono, Consolas"), Padding = new Thickness(6) };
                    c.SetResourceReference(Block.BackgroundProperty, "Surface2"); doc.Blocks.Add(c); break;
                case MdKind.Rule: doc.Blocks.Add(new Paragraph { BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = Brushes.Gray }); break;
                case MdKind.Table: doc.Blocks.Add(BuildTable(b.Rows!)); break;
            }
        }
        return doc;
    }

    private static Paragraph Para(string text)
    {
        var p = new Paragraph { Margin = new Thickness(0, 2, 0, 6) };
        foreach (var (t, bold, code) in MarkdownBlocks.Inline(text))
        {
            var r = new Run(t);
            if (bold) r.FontWeight = FontWeights.Bold;
            if (code) { r.FontFamily = new FontFamily("Cascadia Mono, Consolas"); r.SetResourceReference(TextElement.BackgroundProperty, "Surface2"); }
            p.Inlines.Add(r);
        }
        return p;
    }

    private static Table BuildTable(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var t = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 10), BorderThickness = new Thickness(1) };
        t.SetResourceReference(Table.BorderBrushProperty, "Border");
        var cols = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        for (int i = 0; i < cols; i++) t.Columns.Add(new TableColumn());
        var g = new TableRowGroup(); t.RowGroups.Add(g);
        for (int r = 0; r < rows.Count; r++)
        {
            var row = new TableRow();
            if (r == 0) { row.FontWeight = FontWeights.SemiBold; row.SetResourceReference(TableRow.BackgroundProperty, "Control"); }
            else if (r % 2 == 0) row.SetResourceReference(TableRow.BackgroundProperty, "RowAlt");
            for (int c = 0; c < cols; c++)
            {
                var cell = new TableCell(Para(c < rows[r].Count ? rows[r][c] : "")) { Padding = new Thickness(6, 2, 6, 2), BorderThickness = new Thickness(0, 0, 1, 1) };
                cell.SetResourceReference(TableCell.BorderBrushProperty, "Border");
                row.Cells.Add(cell);
            }
            g.Rows.Add(row);
        }
        return t;
    }
}
