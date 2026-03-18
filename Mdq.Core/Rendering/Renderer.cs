using System.Text;
using Mdq.Core.DocumentModel;

namespace Mdq.Core.Rendering;

public static class Renderer
{
    private static int _listIndent = 0;

    public static string Render(List<MatchableItem> items)
    {
        _listIndent = 0;
        var sb = new StringBuilder();
        RenderItems(items, sb);
        return sb.ToString();
    }

    private static void RenderItems(List<MatchableItem> items, StringBuilder sb)
    {
        // TODO: Eventually should be able to render to different formats (json, html, etc)
        if (items.Count == 0)
            return;

        RenderItem(items[0], sb);
        foreach (var item in items.Skip(1))
        {
            sb.Append("\n\n");
            RenderItem(item, sb);
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
                sb.Append($"{new string('#', heading.Level)} {heading.Text ?? string.Empty}");
                break;

            case TextBlock tb:
                sb.Append(tb.Content);
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
        }
    }

    private static void RenderSection(Section section, StringBuilder sb)
    {
        sb.Append($"{new string('#', section.Heading.Level)} {section.Heading.Text ?? string.Empty}").AppendLine().AppendLine();

        RenderItems(section.Paragraphs.Cast<MatchableItem>().Concat(section.Children.Cast<MatchableItem>()).ToList(), sb);
    }

    private static void RenderBlockQuote(BlockQuote bq, StringBuilder sb)
    {
        foreach (var line in bq.Content.Split('\n'))
            sb.Append($"> {line}").AppendLine();
    }

    private static void RenderListBlock(ListBlock listBlock, StringBuilder sb)
    {
        for (int i = 0; i < listBlock.Items.Count; i++)
        {
            if (sb.Length > 0)
                sb.Append('\n');

            var item = listBlock.Items[i];
            RenderListItem(item, sb);
        }
    }

    private static void RenderListItem(ListItem item, StringBuilder sb)
    {
        string bullet = item.Kind == ListKind.Numbered ? $"{item.Index}." : "-";
        sb.Append($"{new string(' ', _listIndent * 2)}{bullet} {item.Content}");

        if (item.SubList is not null)
        {
            sb.Append('\n');
            _listIndent++;
            RenderItem(item.SubList, sb);
            _listIndent--;
        }
    }
}
