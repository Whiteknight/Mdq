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

    // -------------------------------------------------------------------------
    // Segment dispatch
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes segments starting at <paramref name="segmentIndex"/> against a list of
    /// candidate sections at <paramref name="currentLevel"/>.
    /// </summary>
    private static Result<List<MatchableItem>, QueryError> ExecuteSegments(
        IEnumerable<MatchableItem> items,
        IReadOnlyList<SelectorSegment> selectors)
    {
        var current = items.ToList();
        foreach (var selector in selectors)
        {
            current = selector switch
            {
                SelectorSegment.Heading h => ResolveHeading(h, current),
                SelectorSegment.Text => ResolveText(current),
                SelectorSegment.HeadingContent => ResolveHeadingContent(current),
                SelectorSegment.ParagraphAt p => ResolveParagraph(p, current),
                SelectorSegment.ItemAt item => ResolveItem(item, current),
                _ => throw new Exception($"Unknown selector type: {selector.GetType().Name}")
            };
            if (current.Count == 0)
                return new List<MatchableItem>();
        }

        return current;
    }

    private static List<MatchableItem> ResolveHeading(
        SelectorSegment.Heading selector,
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

    private static List<MatchableItem> ResolveText(List<MatchableItem> items)
    {
        return items
            .SelectMany(i => i switch
            {
                Section s => s.Paragraphs,
                Paragraph p => [p],
                _ => []
            })
            .Cast<MatchableItem>()
            .ToList();
    }

    // -------------------------------------------------------------------------
    // .heading
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveHeadingContent(List<MatchableItem> items)
        => items.OfType<Section>()
            .Select(s => s.Heading)
            .Cast<MatchableItem>()
            .ToList();

    // -------------------------------------------------------------------------
    // .paragraph(N)
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveParagraph(
        SelectorSegment.ParagraphAt paragraphSeg,
        List<MatchableItem> items)
    {
        // TODO: This all goes much nicer if paragraphs have their own index information.
        // Because then we can match Section.Paragraph[n] AND any Paragraph items already in the matchlist with Index N
        return items.OfType<Section>()
            .SelectMany(s => s.Paragraphs.Skip(paragraphSeg.Index - 1).Take(1).Cast<MatchableItem>())
            .ToList();
    }

    // -------------------------------------------------------------------------
    // .item(N)
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ResolveItem(
        SelectorSegment.ItemAt itemSeg,
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
}
