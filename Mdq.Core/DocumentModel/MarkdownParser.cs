using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Mdq.Core.Shared;

namespace Mdq.Core.DocumentModel;

public static class MarkdownParser
{
    public static Result<MarkdownDocument, MdqError> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new MarkdownDocument([]);

        var markdigDoc = Markdig.Markdown.Parse(markdown);
        var flatSections = BuildFlatSections(markdigDoc);
        var tree = BuildSectionTree(flatSections);
        return new MarkdownDocument(tree);
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
        int paragraphIndex = 1;

        foreach (var block in doc)
        {
            if (block is HeadingBlock heading)
            {
                sections.Add(current);
                current = new FlatSection(ExtractInlineText(heading.Inline), heading.Level, []);
                paragraphIndex = 1;
                continue;
            }

            var paragraph = MapBlockToParagraph(block, paragraphIndex);
            if (paragraph is not null)
            {
                current.Paragraphs.Add(paragraph);
                paragraphIndex++;
            }
        }

        sections.Add(current);
        return sections;
    }

    private static Paragraph? MapBlockToParagraph(Block block, int paragraphIndex)
        => block switch
        {
            ParagraphBlock p => new TextBlock(ExtractInlineText(p.Inline), paragraphIndex),
            Markdig.Syntax.ListBlock lb => MapListBlock(lb, paragraphIndex),
            QuoteBlock qb => new BlockQuote(ExtractQuoteText(qb), paragraphIndex),
            Markdig.Syntax.FencedCodeBlock fcb => new CodeBlock(fcb.Info, ExtractLineText(fcb.Lines), paragraphIndex),
            Markdig.Syntax.CodeBlock cb => new CodeBlock(null, ExtractLineText(cb.Lines), paragraphIndex),
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

    private static Section ToSection(FlatSection flat, List<Section> children)
        => new Section(new Heading(flat.HeadingText, flat.HeadingLevel), flat.Paragraphs, children);

    // -------------------------------------------------------------------------
    // List mapping
    // -------------------------------------------------------------------------

    private static ListBlock MapListBlock(Markdig.Syntax.ListBlock lb, int paragraphIndex)
    {
        var kind = lb.IsOrdered ? ListKind.Numbered : ListKind.Bulleted;
        var items = lb
            .OfType<ListItemBlock>()
            .Select((lib, index) => MapListItem(lib, kind, index + 1))
            .ToList();
        return new ListBlock(kind, items, paragraphIndex);
    }

    private static ListItem MapListItem(ListItemBlock lib, ListKind kind, int index)
    {
        var subList = lib.OfType<Markdig.Syntax.ListBlock>().FirstOrDefault();
        var textContent = lib
            .OfType<ParagraphBlock>()
            .Select(p => ExtractInlineText(p.Inline))
            .FirstOrDefault() ?? string.Empty;

        var mappedSubList = subList is not null ? MapListBlock(subList, 1) : null;
        return new ListItem(textContent, kind, index, mappedSubList);
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
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private static string ExtractLineText(StringLineGroup lines)
    {
        return string.Join(Environment.NewLine, lines.Lines).Trim();
    }
}
