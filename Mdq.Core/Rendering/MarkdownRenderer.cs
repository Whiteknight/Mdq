using System.Text;
using Mdq.Core.DocumentModel;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.Rendering;

public class MarkdownRenderer : IRenderer
{
    private int _listIndent;

    public string Render(List<MatchableItem> items)
    {
        _listIndent = 0;
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
        }
    }

    private static void RenderHeading(Heading heading, StringBuilder sb)
    {
        AppendSegment(sb, heading.LeadingTrivia);
        sb.Append($"{Str(heading.Text)}");
        AppendSegment(sb, heading.TrailingTrivia);
    }

    private void RenderSection(Section section, StringBuilder sb)
    {
        RenderHeading(section.Heading, sb);
        foreach (var para in section.Paragraphs)
            RenderItem(para, sb);
        foreach (var child in section.Children)
            RenderItem(child, sb);
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

    private void RenderListBlock(ListBlock listBlock, StringBuilder sb)
    {
        for (int i = 0; i < listBlock.Items.Count; i++)
        {
            var item = listBlock.Items[i];
            RenderListItem(item, sb);
        }
        sb.AppendLine();
    }

    private void RenderListItem(ListItem item, StringBuilder sb)
    {
        sb.Append($"{Str(item.LeadingTrivia)}{Str(item.Content)}{Str(item.TrailingTrivia)}");

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
        sb.Append(Str(cb.Content));
        sb.Append("```");
    }

    private static void RenderTableRow(TableRow tr, StringBuilder sb)
    {
        AppendSegment(sb, tr.LeadingTrivia);
        AppendSegment(sb, tr.TrailingTrivia);
    }

    private static void RenderTableBlock(TableBlock tb, StringBuilder sb)
    {
        AppendSegment(sb, tb.LeadingTrivia);
        AppendSegment(sb, tb.TrailingTrivia);
    }

    private static string Str(StringSegment segment) => segment.HasValue ? segment.Value! : string.Empty;

    private static void AppendSegment(StringBuilder sb, StringSegment segment)
    {
        if (segment.HasValue && segment.Length > 0)
            sb.Append(segment.Value);
    }
}
