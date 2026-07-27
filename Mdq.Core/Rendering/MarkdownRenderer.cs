using System.Text;
using Mdq.Core.DocumentModel;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.Rendering;

public class MarkdownRenderer : IRenderer
{
    public string Render(List<MatchableItem> items)
    {
        var sb = new StringBuilder();
        RenderItems(items, sb);
        return sb.ToString();
    }

    private void RenderItems(List<MatchableItem> items, StringBuilder sb)
    {
        if (items.Count == 0)
            return;

        foreach (var item in items)
            RenderItem(item, sb);
    }

    private void RenderItem(MatchableItem item, StringBuilder sb)
    {
        switch (item)
        {
            case MarkdownDocument md:
                RenderItems(md.TopLevelSection.Children.Cast<MatchableItem>().ToList(), sb);
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

            case TableCell tc:
                RenderTableCell(tc, sb);
                break;
        }
    }

    private static void RenderHeading(Heading heading, StringBuilder sb)
    {
        AppendSegment(sb, heading.LeadingTrivia);
        AppendSegment(sb, heading.Text);
        AppendSegment(sb, heading.TrailingTrivia);
    }

    private void RenderSection(Section section, StringBuilder sb)
    {
        AppendSegment(sb, section.LeadingTrivia);
        RenderHeading(section.Heading, sb);
        foreach (var para in section.Paragraphs)
            RenderItem(para, sb);
        foreach (var child in section.Children)
            RenderItem(child, sb);
        AppendSegment(sb, section.TrailingTrivia);
    }

    private static void RenderTextBlock(TextBlock tb, StringBuilder sb)
    {
        AppendSegment(sb, tb.LeadingTrivia);
        AppendSegment(sb, tb.Content);
        AppendSegment(sb, tb.TrailingTrivia);
    }

    private static readonly char[] _lineSeparators = ['\n'];

    private static void RenderBlockQuote(BlockQuote bq, StringBuilder sb)
    {
        AppendSegment(sb, bq.LeadingTrivia);
        var lines = bq.Content.Split(_lineSeparators);
        foreach (var line in lines)
        {
            sb.Append("> ");
            AppendSegment(sb, line);
            sb.AppendLine();
        }
        AppendSegment(sb, bq.TrailingTrivia);
    }

    private void RenderListBlock(ListBlock listBlock, StringBuilder sb)
    {
        AppendSegment(sb, listBlock.LeadingTrivia);
        for (int i = 0; i < listBlock.Items.Count; i++)
        {
            var item = listBlock.Items[i];
            RenderListItem(item, sb);
        }
        AppendSegment(sb, listBlock.TrailingTrivia);
    }

    private void RenderListItem(ListItem item, StringBuilder sb)
    {
        AppendSegment(sb, item.LeadingTrivia);
        AppendSegment(sb, item.Content);
        AppendSegment(sb, item.TrailingTrivia);
        if (item.SubList is not null)
            RenderItem(item.SubList, sb);
    }

    private static void RenderCodeBlock(CodeBlock cb, StringBuilder sb)
    {
        if (cb.Fenced)
        {
            AppendSegment(sb, cb.LeadingTrivia);
            //sb.Append("```");
            AppendSegment(sb, cb.Language);
            sb.AppendLine();
            foreach (var line in cb.Lines)
            {
                AppendSegment(sb, line);
                sb.AppendLine();
            }
            // TODO: Closing fence should be part of TrailingTrivia
            sb.Append("```");
            AppendSegment(sb, cb.TrailingTrivia);
        }
        else
        {
            foreach (var line in cb.Lines)
            {
                AppendSegment(sb, cb.Indent);
                AppendSegment(sb, line);
                sb.AppendLine();
            }
        }
    }

    private static void RenderTableRow(TableRow tr, StringBuilder sb)
    {
        AppendSegment(sb, tr.LeadingTrivia);
        foreach (var cell in tr.Cells)
        {
            // TODO: We should include the | and all the whitespace in the cell's trivia, so we
            // can simplify all this.
            sb.Append("| ");
            AppendSegment(sb, cell.Content);
            sb.Append(' ');
        }
        sb.Append('|');
        AppendSegment(sb, tr.TrailingTrivia);
    }

    private static void RenderTableRow(TableRow tr, StringBuilder sb, List<int> cellWidths)
    {
        AppendSegment(sb, tr.LeadingTrivia);
        for (int i = 0; i < tr.Cells.Count; i++)
        {
            var cell = tr.Cells[i];
            sb.Append("| ");
            AppendSegment(sb, cell.Content);
            var padLength = cellWidths[i] - cell.Content.Length;
            for (int j = 0; j < padLength; j++)
                sb.Append(' ');
            sb.Append(' ');
        }
        sb.Append('|');
        AppendSegment(sb, tr.TrailingTrivia);
    }

    private static void RenderTableBlock(TableBlock tb, StringBuilder sb)
    {
        AppendSegment(sb, tb.LeadingTrivia);

        var widths = tb.Header.Cells.Select(c => c.Content.Length).ToList();
        foreach (var row in tb.Rows)
        {
            for (int i = 0; i < row.Cells.Count; i++)
            {
                if (i < widths.Count)
                    widths[i] = Math.Max(widths[i], row.Cells[i].Content.Length);
                else
                    widths.Add(row.Cells[i].Content.Length);
            }
        }

        RenderTableRow(tb.Header, sb, widths);
        foreach (var width in widths)
            sb.Append($"| {new string('-', width)} ");
        sb.Append('|').AppendLine();
        foreach (var row in tb.Rows)
            RenderTableRow(row, sb, widths);
        AppendSegment(sb, tb.TrailingTrivia);
    }

    private static void RenderTableCell(TableCell tc, StringBuilder sb)
    {
        // TODO: We need two methods for rendering cells.
        // If we render as part of a table we should include the trivia. Otherwise we should omit it.
        AppendSegment(sb, tc.LeadingTrivia);
        AppendSegment(sb, tc.Content);
        AppendSegment(sb, tc.TrailingTrivia);
        // If we render cells by themselves we should add a newline to separate them
        // If we render cells as part of a row we should not embed newlines
        sb.AppendLine();
    }

    private static void AppendSegment(StringBuilder sb, StringSegment segment)
    {
        if (!segment.HasValue || segment.Length == 0)
            return;
        for (int i = 0; i < segment.Length; i++)
            sb.Append(segment[i]);
    }
}
