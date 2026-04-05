using System.Text;
using Mdq.Core.DocumentModel;
using Mdq.Core.Editing;

namespace Mdq.Core.Rendering;

/// <summary>
/// Renders a full <see cref="MarkdownDocument"/> to Markdown, applying a single
/// inline mutation when the resolved target node is encountered during traversal.
/// Identity is checked with <see cref="ReferenceEquals"/> -- never record equality.
/// </summary>
public sealed class EditingMarkdownRenderer
{
    private readonly IReadOnlyList<MatchableItem> _resolvedTargets;
    private readonly EditOperation _operation;
    private int _listIndent;

    public EditingMarkdownRenderer(MatchableItem target, EditOperation operation)
        : this([target], operation) { }

    public EditingMarkdownRenderer(IReadOnlyList<MatchableItem> targets, EditOperation operation)
    {
        _resolvedTargets = targets
            .Select(t => t is SyntheticTextBlock synthetic ? synthetic.Source : t)
            .ToList();
        _operation = operation;
    }

    public string Render(MarkdownDocument document)
    {
        _listIndent = 0;
        var sb = new StringBuilder();
        RenderItems(document.Sections.Cast<MatchableItem>().ToList(), sb);
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Tree traversal
    // -------------------------------------------------------------------------

    private void RenderItems(List<MatchableItem> items, StringBuilder sb)
    {
        if (items.Count == 0)
            return;

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

    private void RenderItem(MatchableItem item, StringBuilder sb)
    {
        switch (item)
        {
            case Section section:
                RenderSection(section, sb);
                break;

            case Heading heading:
                sb.Append($"{new string('#', heading.Level)} {heading.Text ?? string.Empty}");
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
        }
    }

    // -------------------------------------------------------------------------
    // Section
    // -------------------------------------------------------------------------

    private void RenderSection(Section section, StringBuilder sb)
    {
        var headingText = IsTarget(section)
            ? MutatedSectionHeadingText(section)
            : section.Heading.Text ?? string.Empty;

        sb.Append($"{new string('#', section.Heading.Level)} {headingText}").AppendLine().AppendLine();

        var children = section.Paragraphs.Cast<MatchableItem>()
            .Concat(section.Children.Cast<MatchableItem>())
            .ToList();

        RenderItems(children, sb);
    }

    private string MutatedSectionHeadingText(Section section)
        => _operation is Set set ? set.Text : section.Heading.Text ?? string.Empty;

    // -------------------------------------------------------------------------
    // TextBlock
    // -------------------------------------------------------------------------

    private void RenderTextBlock(TextBlock tb, StringBuilder sb)
    {
        if (!IsTarget(tb))
        {
            sb.Append(tb.Content);
            return;
        }

        var content = _operation switch
        {
            Add add => $"{tb.Content} {add.Text}",
            Set set => set.Text,
            _ => tb.Content
        };

        sb.Append(content);
    }

    // -------------------------------------------------------------------------
    // BlockQuote
    // -------------------------------------------------------------------------

    private void RenderBlockQuote(BlockQuote bq, StringBuilder sb)
    {
        var content = IsTarget(bq) && _operation is Add add
            ? $"{bq.Content} {add.Text}"
            : bq.Content;

        foreach (var line in content.Split('\n'))
            sb.Append($"> {line}").AppendLine();
    }

    // -------------------------------------------------------------------------
    // ListBlock
    // -------------------------------------------------------------------------

    private void RenderListBlock(ListBlock lb, StringBuilder sb)
    {
        var items = IsTarget(lb) && _operation is Add addOp
            ? ItemsWithAppended(lb, addOp.Text)
            : lb.Items;

        for (int i = 0; i < items.Count; i++)
        {
            if (i > 0)
                sb.AppendLine();
            RenderListItem(items[i], sb);
        }
    }

    private static IReadOnlyList<ListItem> ItemsWithAppended(ListBlock lb, string text)
    {
        var newItem = new ListItem(text, lb.Kind, lb.Items.Count + 1, null);
        return lb.Items.Append(newItem).ToList();
    }

    // -------------------------------------------------------------------------
    // ListItem
    // -------------------------------------------------------------------------

    private void RenderListItem(ListItem item, StringBuilder sb)
    {
        var content = IsTarget(item) && _operation is Set set
            ? set.Text
            : item.Content;

        string bullet = item.Kind == ListKind.Numbered ? $"{item.Index}." : "-";
        sb.Append($"{new string(' ', _listIndent * 2)}{bullet} {content}");

        if (item.SubList is not null)
        {
            sb.AppendLine();
            _listIndent++;
            RenderItem(item.SubList, sb);
            _listIndent--;
        }
    }

    // -------------------------------------------------------------------------
    // CodeBlock
    // -------------------------------------------------------------------------

    private void RenderCodeBlock(CodeBlock cb, StringBuilder sb)
    {
        var content = IsTarget(cb) && _operation is Add add
            ? $"{cb.Content}\n{add.Text}"
            : cb.Content;

        sb.AppendLine($"```{cb.Language}");
        sb.AppendLine(content);
        sb.AppendLine("```");
    }

    // -------------------------------------------------------------------------
    // Identity check
    // -------------------------------------------------------------------------

    private bool IsTarget(MatchableItem candidate)
        => _resolvedTargets.Any(t => ReferenceEquals(candidate, t));
}
