using System.Text;
using Mdq.Core.DocumentModel;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.Rendering;

public class MarkdownRenderer : IRenderer
{
    private static int _listIndent = 0;

    public string Render(List<MatchableItem> items)
    {
        _listIndent = 0;
        var sb = new StringBuilder();
        RenderItems(items, sb);
        return sb.ToString();
    }

    private static bool HasTrivia(MatchableItem item) =>
        (item.LeadingTrivia.HasValue && item.LeadingTrivia.Length > 0) ||
        (item.TrailingTrivia.HasValue && item.TrailingTrivia.Length > 0);

    private static void RenderItems(List<MatchableItem> items, StringBuilder sb)
    {
        if (items.Count == 0)
            return;

        // If any item has trivia, use trivia-based rendering (no synthetic separators)
        if (items.Any(HasTrivia))
        {
            foreach (var item in items)
                RenderItem(item, sb);
            return;
        }

        // Synthetic rendering: add separators between items
        RenderItem(items[0], sb);
        var lastItem = items[0];
        foreach (var item in items.Skip(1))
        {
            sb.AppendLine();
            if (lastItem is Paragraph || lastItem is Section || lastItem is Heading)
                sb.AppendLine();
            RenderItem(item, sb);
            lastItem = item;
        }
    }

    private static void RenderItem(MatchableItem item, StringBuilder sb)
    {
        switch (item)
        {
            case MarkdownDocument md:
                RenderItems(md.Sections.Cast<MatchableItem>().ToList(), sb);
                break;

            case Section section:
                RenderSection(section, sb);
                break;

            case Heading heading:
                RenderHeading(heading, sb);
                break;

            case TextBlock tb:
                RenderTextBlock(tb, sb);
                break;

            case BlockQuote bq:
                RenderBlockQuote(bq, sb);
                break;

            case ListBlock lb:
                RenderListBlock(lb, sb);
                break;

            case ListItem li:
                RenderListItem(li, sb);
                break;

            case CodeBlock cb:
                RenderCodeBlock(cb, sb);
                break;

            case TableRow tr:
                RenderTableRow(tr, sb);
                break;

            case TableBlock tb:
                RenderTableBlock(tb, sb);
                break;
        }
    }

    private static void RenderHeading(Heading heading, StringBuilder sb)
    {
        AppendSegment(sb, heading.LeadingTrivia);
        sb.Append($"{new string('#', heading.Level)} {Str(heading.Text)}");
        AppendSegment(sb, heading.TrailingTrivia);
    }

    private static void RenderSection(Section section, StringBuilder sb)
    {
        // Check if this section has trivia-based content
        bool hasTrivia = HasTrivia(section.Heading) ||
                         section.Paragraphs.Any(HasTrivia) ||
                         section.Children.Any(HasTrivia);

        if (hasTrivia)
        {
            // Trivia-based: emit heading with its trivia, then paragraphs/children with theirs
            RenderHeading(section.Heading, sb);
            foreach (var para in section.Paragraphs)
                RenderItem(para, sb);
            foreach (var child in section.Children)
                RenderItem(child, sb);
        }
        else
        {
            // Synthetic: old behavior
            sb.Append($"{new string('#', section.Heading.Level)} {Str(section.Heading.Text)}").AppendLine().AppendLine();
            RenderItems(section.Paragraphs.Cast<MatchableItem>().Concat(section.Children.Cast<MatchableItem>()).ToList(), sb);
        }
    }

    private static void RenderTextBlock(TextBlock tb, StringBuilder sb)
    {
        AppendSegment(sb, tb.LeadingTrivia);
        AppendSegment(sb, tb.Content);
        AppendSegment(sb, tb.TrailingTrivia);
    }

    private static void RenderBlockQuote(BlockQuote bq, StringBuilder sb)
    {
        foreach (var line in Str(bq.Content).Split('\n'))
            sb.Append($"> {line}").AppendLine();
    }

    private static void RenderListBlock(ListBlock listBlock, StringBuilder sb)
    {
        for (int i = 0; i < listBlock.Items.Count; i++)
        {
            if (i > 0)
                sb.AppendLine();

            var item = listBlock.Items[i];
            RenderListItem(item, sb);
        }
    }

    private static void RenderListItem(ListItem item, StringBuilder sb)
    {
        string bullet = item.Kind == ListKind.Numbered ? $"{item.Index}." : "-";
        sb.Append($"{new string(' ', _listIndent * 2)}{bullet} {Str(item.Content)}");

        if (item.SubList is not null)
        {
            sb.AppendLine();
            _listIndent++;
            RenderItem(item.SubList, sb);
            _listIndent--;
        }
    }

    private static void RenderCodeBlock(CodeBlock cb, StringBuilder sb)
    {
        sb.AppendLine($"```{cb.Language}");
        sb.AppendLine(Str(cb.Content));
        sb.Append("```");
    }

    private static void RenderTableRow(TableRow tr, StringBuilder sb)
    {
        if (HasTrivia(tr))
        {
            AppendSegment(sb, tr.LeadingTrivia);
            AppendSegment(sb, tr.TrailingTrivia);
            return;
        }

        // Synthetic rendering (Markdig path)
        sb.Append("| ");
        sb.Append(string.Join(" | ", tr.Cells.Select(c => Str(c).PadRight(8))));
        sb.Append(" |");
    }

    private static void RenderTableBlock(TableBlock tb, StringBuilder sb)
    {
        if (HasTrivia(tb))
        {
            AppendSegment(sb, tb.LeadingTrivia);
            AppendSegment(sb, tb.TrailingTrivia);
            return;
        }

        // Synthetic rendering (Markdig path)
        var allRows = new List<TableRow> { tb.Header };
        allRows.AddRange(tb.Rows);

        var colCount = tb.Header.Cells.Count;
        var widths = new int[colCount];
        foreach (var row in allRows)
            for (int i = 0; i < row.Cells.Count && i < colCount; i++)
                widths[i] = Math.Max(widths[i], Str(row.Cells[i]).Length);

        // Render header
        sb.Append("| ");
        sb.Append(string.Join(" | ", tb.Header.Cells.Select((c, i) => Str(c).PadRight(widths[i]))));
        sb.AppendLine(" |");

        // Render separator
        sb.Append("| ");
        sb.Append(string.Join(" | ", widths.Select(w => new string('-', w))));
        sb.AppendLine(" |");

        // Render data rows
        for (int r = 0; r < tb.Rows.Count; r++)
        {
            var row = tb.Rows[r];
            sb.Append("| ");
            sb.Append(string.Join(" | ", row.Cells.Select((c, i) => Str(c).PadRight(widths[i]))));
            sb.Append(" |");
            if (r < tb.Rows.Count - 1)
                sb.AppendLine();
        }
    }

    private static string Str(StringSegment segment) => segment.HasValue ? segment.Value! : string.Empty;

    private static void AppendSegment(StringBuilder sb, StringSegment segment)
    {
        if (segment.HasValue && segment.Length > 0)
            sb.Append(segment.Value);
    }
}
