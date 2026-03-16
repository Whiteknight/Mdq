using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Mdq.Core.Shared;

namespace Mdq.Core.DocumentModel;

public static class MarkdownParser
{
    public static Result<MarkdownDocument, MarkdownParseError> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new Result<MarkdownDocument, MarkdownParseError>.Ok(new MarkdownDocument([]));

        var markdigDoc = Markdig.Markdown.Parse(markdown);
        var flatSections = BuildFlatSections(markdigDoc);
        var tree = BuildSectionTree(flatSections);
        return new Result<MarkdownDocument, MarkdownParseError>.Ok(new MarkdownDocument(tree));
    }

    // -------------------------------------------------------------------------
    // Phase 1: flatten the Markdig block list into a sequence of (heading, paragraphs)
    // -------------------------------------------------------------------------

    private sealed record FlatSection(
        string? HeadingText,
        int HeadingLevel,
        List<Paragraph> Paragraphs);

    private static List<FlatSection> BuildFlatSections(Markdig.Syntax.MarkdownDocument doc)
    {
        var sections = new List<FlatSection>();
        var current = new FlatSection(null, 0, []);

        foreach (var block in doc)
        {
            if (block is HeadingBlock heading)
            {
                sections.Add(current);
                current = new FlatSection(ExtractInlineText(heading.Inline), heading.Level, []);
                continue;
            }

            var paragraph = MapBlockToParagraph(block);
            if (paragraph is not null)
                current.Paragraphs.Add(paragraph);
        }

        sections.Add(current);
        return sections;
    }

    private static Paragraph? MapBlockToParagraph(Block block) => block switch
    {
        ParagraphBlock p => new Paragraph.TextBlock(ExtractInlineText(p.Inline)),
        Markdig.Syntax.ListBlock lb => MapListBlock(lb),
        QuoteBlock qb => new Paragraph.BlockQuote(ExtractQuoteText(qb)),
        _ => null
    };

    // -------------------------------------------------------------------------
    // Phase 2: nest flat sections into a tree based on heading level
    // -------------------------------------------------------------------------

    private static IReadOnlyList<Section> BuildSectionTree(List<FlatSection> flat)
    {
        // Stack holds (section-being-built, its mutable children list).
        // We use a virtual root at level 0 to simplify the algorithm.
        var rootChildren = new List<Section>();
        var stack = new Stack<(FlatSection Flat, List<Section> Children)>();

        // Push a sentinel root so we always have a parent to attach to.
        var sentinel = new FlatSection(null, 0, []);
        stack.Push((sentinel, rootChildren));

        foreach (var section in flat)
        {
            // The preamble (HeadingLevel == 0) is always a root-level section.
            if (section.HeadingLevel == 0)
            {
                // Only emit preamble if it has content.
                if (section.Paragraphs.Count > 0)
                    rootChildren.Add(ToSection(section, []));
                continue;
            }

            // Pop stack entries that are at the same level or deeper.
            while (stack.Count > 1 && stack.Peek().Flat.HeadingLevel >= section.HeadingLevel)
            {
                var (popped, poppedChildren) = stack.Pop();
                var built = ToSection(popped, poppedChildren);
                stack.Peek().Children.Add(built);
            }

            var children = new List<Section>();
            stack.Push((section, children));
        }

        // Drain remaining stack entries (except sentinel).
        while (stack.Count > 1)
        {
            var (popped, poppedChildren) = stack.Pop();
            var built = ToSection(popped, poppedChildren);
            stack.Peek().Children.Add(built);
        }

        return rootChildren;
    }

    private static Section ToSection(FlatSection flat, List<Section> children) =>
        new(flat.HeadingText, flat.HeadingLevel, flat.Paragraphs, children);

    // -------------------------------------------------------------------------
    // List mapping
    // -------------------------------------------------------------------------

    private static Paragraph.ListBlock MapListBlock(Markdig.Syntax.ListBlock lb)
    {
        var kind = lb.IsOrdered ? ListKind.Numbered : ListKind.Bulleted;
        var items = lb
            .OfType<ListItemBlock>()
            .Select(MapListItem)
            .ToList();
        return new Paragraph.ListBlock(kind, items);
    }

    private static ListItem MapListItem(ListItemBlock lib)
    {
        var subList = lib.OfType<Markdig.Syntax.ListBlock>().FirstOrDefault();
        var textContent = lib
            .OfType<ParagraphBlock>()
            .Select(p => ExtractInlineText(p.Inline))
            .FirstOrDefault() ?? string.Empty;

        var mappedSubList = subList is not null ? MapListBlock(subList) : null;
        return new ListItem(textContent, mappedSubList);
    }

    // -------------------------------------------------------------------------
    // Text extraction helpers
    // -------------------------------------------------------------------------

    private static string ExtractInlineText(ContainerInline? container)
    {
        if (container is null)
            return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var inline in container)
            AppendInlineText(inline, sb);
        return sb.ToString().Trim();
    }

    private static void AppendInlineText(Inline inline, System.Text.StringBuilder sb)
    {
        if (inline is LiteralInline literal)
        {
            sb.Append(literal.Content.ToString());
            return;
        }

        if (inline is LineBreakInline)
        {
            sb.Append(' ');
            return;
        }

        if (inline is ContainerInline container)
        {
            foreach (var child in container)
                AppendInlineText(child, sb);
        }
    }

    private static string ExtractQuoteText(QuoteBlock qb)
    {
        var lines = qb
            .OfType<ParagraphBlock>()
            .Select(p => ExtractInlineText(p.Inline));
        return string.Join("\n", lines).Trim();
    }
}
