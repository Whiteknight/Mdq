using Mdq.Core.DocumentModel;
using Mdq.Core.SelectorModel;
using Mdq.Core.Shared;

namespace Mdq.Core.QueryEngine;

/// <summary>
/// Evaluates a <see cref="SelectorChain"/> against a <see cref="MarkdownDocument"/>
/// and returns the matched content as a Markdown string.
/// </summary>
public static class QueryExecutor
{
    // -------------------------------------------------------------------------
    // Public entry point
    // -------------------------------------------------------------------------

    public static Result<List<MatchableItem>, MdqError> Execute(MarkdownDocument document, SelectorChain selectors)
    {
        if (selectors.IsEmpty)
            return new List<MatchableItem>() { document };

        return ExecuteSegments(new List<MatchableItem>() { document }, selectors.Segments)
            .MapError(e => (MdqError)e);
    }

    /// <summary>
    /// Processes segments starting at <paramref name="segmentIndex"/> against a list of
    /// candidate sections at <paramref name="currentLevel"/>.
    /// </summary>
    private static Result<List<MatchableItem>, QueryError> ExecuteSegments(
        IEnumerable<MatchableItem> items,
        IReadOnlyList<Selector> selectors)
    {
        var current = items.ToList();
        foreach (var selector in selectors)
        {
            current = selector switch
            {
                Selector.Heading h => ResolvePoundHeading(h, current),
                Selector.Text => ResolveDotText(current),
                Selector.HeadingContent => ResolveDotHeading(current),
                Selector.ParagraphAt p => ResolveDotParagraphN(p, current),
                Selector.ItemAt item => ResolveDotItemN(item, current),
                Selector.Items => ResolveDotItems(current),
                Selector.Filter f => ResolveFilter(f, current),
                Selector.Flatten f => ResolveFlatten(f, current),
                Selector.SkipTake st => ResolveSkipTake(st, current),
                _ => throw new Exception($"Unknown selector type: {selector.GetType().Name}")
            };
            if (current.Count == 0)
                return new List<MatchableItem>();
        }

        return current;
    }

    private static List<MatchableItem> ResolvePoundHeading(
        Selector.Heading selector,
        List<MatchableItem> items)
    {
        return items
            .SelectMany(i => i switch
            {
                MarkdownDocument d => d.Sections.Where(s => s.Heading.IsMatch(selector.Name)),
                Section s => s.Children.Where(c => c.Heading.IsMatch(selector.Name)),
                _ => []
            })
            .Cast<MatchableItem>()
            .ToList();
    }

    // -------------------------------------------------------------------------
    // .text
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveDotText(List<MatchableItem> items)
    {
        return items
            .SelectMany(i => i switch
            {
                MarkdownDocument md => md.Sections[0].Paragraphs.Cast<MatchableItem>(),
                Section s => s.Paragraphs.Cast<MatchableItem>(),
                Heading h and { Text: { } } => [new TextBlock(h.Text, 1)],
                ListItem li => [new TextBlock(li.Content, 1)],
                CodeBlock cb => [new TextBlock(cb.Content, 1)],
                // TODO: Should a Paragraph here (besides the CodeBlock) resolve to itself?
                _ => []
            })
            .ToList();
    }

    // -------------------------------------------------------------------------
    // .heading
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveDotHeading(List<MatchableItem> items)
        => items.OfType<Section>()
            .Select(s => s.Heading)
            .Cast<MatchableItem>()
            .ToList();

    // -------------------------------------------------------------------------
    // .paragraph(N)
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveDotParagraphN(
        Selector.ParagraphAt paragraphSeg,
        List<MatchableItem> items)
    {
        return items
            .SelectMany(i => i switch
            {
                MarkdownDocument md => md.Sections[0].Paragraphs.Where(p => p.Index == paragraphSeg.Index),
                Section s => s.Paragraphs.Where(p => p.Index == paragraphSeg.Index),
                _ => []
            })
            .Cast<MatchableItem>()
            .ToList();
    }

    // -------------------------------------------------------------------------
    // .item(N) and .items
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveDotItemN(
        Selector.ItemAt itemSeg,
        List<MatchableItem> items)
    {
        return items
            .SelectMany(i => i switch
            {
                ListItem li => (li.SubList?.Items ?? []).Where(li => li.Index == itemSeg.Index).Cast<MatchableItem>(),
                ListBlock lb => lb.Items.Where(li => li.Index == itemSeg.Index).Cast<MatchableItem>(),
                Section s => s.Paragraphs.Take(1).OfType<ListBlock>().SelectMany(lb => lb.Items.Where(li => li.Index == itemSeg.Index).Cast<MatchableItem>()),
                _ => []
            })
            .ToList();
    }

    private static List<MatchableItem> ResolveDotItems(
        List<MatchableItem> items)
    {
        return items
            .SelectMany(i => i switch
            {
                ListBlock lb => lb.Items.Cast<MatchableItem>(),
                ListItem li => [li],
                _ => []
            })
            .ToList();
    }

    // -------------------------------------------------------------------------
    // .flatten
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveFlatten(Selector.Flatten f, List<MatchableItem> current)
    {
        return current
            .SelectMany(Flatten)
            .ToList();
    }

    private static IEnumerable<MatchableItem> Flatten(MatchableItem item)
    {
        switch (item)
        {
            case MarkdownDocument md:
                return md.Sections.SelectMany(Flatten);

            case Section s:
                return (s.Heading?.Text != null ? [s.Heading] : Array.Empty<MatchableItem>())
                    .Concat(s.Paragraphs.SelectMany(p => Flatten(p)))
                    .Concat(s.Children.SelectMany(Flatten));

            case ListBlock lb:
                return lb.Items.SelectMany(Flatten);

            case ListItem li:
                return new MatchableItem[] { li }
                    .Concat(li.SubList != null ? Flatten(li.SubList) : Array.Empty<MatchableItem>());

            case MatchableItem mi:
                return [mi];
        }
        return [];
    }

    // -------------------------------------------------------------------------
    // .skip(n) and .take(n)
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveSkipTake(Selector.SkipTake st, List<MatchableItem> current)
    {
        return current.Skip(st.Skip).Take(st.Take == 0 ? current.Count - st.Skip : st.Take).ToList();
    }

    // -------------------------------------------------------------------------
    // [property=value]
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveFilter(Selector.Filter f, List<MatchableItem> current)
    {
        return current.Where(i => i.IsMatch(f.Property, f.Operator, f.Value)).ToList();
    }
}
