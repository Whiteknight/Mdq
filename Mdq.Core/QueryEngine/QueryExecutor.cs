using System.Text;
using Mdq.Core.DocumentModel;
using Mdq.Core.Shared;
using Mdq.Core.SelectorModel;

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

    public static Result<string, QueryError> Execute(MarkdownDocument document, SelectorChain chain)
    {
        if (chain.IsEmpty)
            return Ok(RenderDocument(document));

        return ExecuteSegments(document.Sections, chain.Segments, currentLevel: 1, segmentIndex: 0);
    }

    // -------------------------------------------------------------------------
    // Segment dispatch
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes segments starting at <paramref name="segmentIndex"/> against a list of
    /// candidate sections at <paramref name="currentLevel"/>.
    /// </summary>
    private static Result<string, QueryError> ExecuteSegments(
        IReadOnlyList<Section> sections,
        IReadOnlyList<SelectorSegment> segments,
        int currentLevel,
        int segmentIndex)
    {
        if (segmentIndex >= segments.Count)
            return Ok(RenderSections(sections));

        var segment = segments[segmentIndex];

        if (segment is SelectorSegment.Heading headingSeg)
            return ResolveHeading(sections, segments, currentLevel, segmentIndex, headingSeg);

        // Non-heading segments require a single section context.
        // If we arrive here without a heading selector having narrowed to one section,
        // treat the full section list as the implicit context.
        if (sections.Count == 1)
            return ExecuteSectionSegment(sections[0], segments, segmentIndex);

        // Multiple sections with a non-heading segment: apply to all and join.
        // (Practically this only occurs when the chain starts with a content selector.)
        var combinedParagraphs = sections.SelectMany(s => s.Paragraphs).ToList();
        return ExecuteSectionSegment(
            new Section(null, 0, combinedParagraphs, []),
            segments,
            segmentIndex);
    }

    private static Result<string, QueryError> ResolveHeading(
        IReadOnlyList<Section> sections,
        IReadOnlyList<SelectorSegment> segments,
        int currentLevel,
        int segmentIndex,
        SelectorSegment.Heading headingSeg)
    {
        var matched = sections.FirstOrDefault(s =>
            string.Equals(s.HeadingText, headingSeg.Name, StringComparison.Ordinal));

        if (matched is null)
            return Fail(new QueryError.HeadingNotFound(headingSeg.Name, currentLevel));

        int nextIndex = segmentIndex + 1;

        if (nextIndex >= segments.Count)
            return Ok(RenderSection(matched));

        var nextSegment = segments[nextIndex];

        if (nextSegment is SelectorSegment.Heading)
            return ExecuteSegments(matched.Children, segments, currentLevel + 1, nextIndex);

        return ExecuteSectionSegment(matched, segments, nextIndex);
    }

    /// <summary>
    /// Dispatches a non-heading segment against a resolved <see cref="Section"/>.
    /// </summary>
    private static Result<string, QueryError> ExecuteSectionSegment(
        Section section,
        IReadOnlyList<SelectorSegment> segments,
        int segmentIndex)
    {
        var segment = segments[segmentIndex];

        return segment switch
        {
            SelectorSegment.Text          => ResolveText(section),
            SelectorSegment.HeadingContent => ResolveHeadingContent(section),
            SelectorSegment.ParagraphAt p  => ResolveParagraph(section, segments, segmentIndex, p),
            SelectorSegment.ItemAt item    => ResolveItemOnSection(section, segments, segmentIndex, item),
            SelectorSegment.Heading h      => Fail(new QueryError.HeadingNotFound(h.Name, section.HeadingLevel + 1)),
            _                              => Fail(new QueryError.HeadingNotFound("(unknown)", 0))
        };
    }

    // -------------------------------------------------------------------------
    // .text
    // -------------------------------------------------------------------------

    private static Result<string, QueryError> ResolveText(Section section)
    {
        var parts = new List<string>();

        foreach (var para in section.Paragraphs)
            parts.Add(RenderParagraph(para));

        foreach (var child in section.Children)
            parts.Add(RenderSection(child));

        return Ok(string.Join("\n\n", parts));
    }

    // -------------------------------------------------------------------------
    // .heading
    // -------------------------------------------------------------------------

    private static Result<string, QueryError> ResolveHeadingContent(Section section) =>
        Ok(section.HeadingText ?? string.Empty);

    // -------------------------------------------------------------------------
    // .paragraph(N)
    // -------------------------------------------------------------------------

    private static Result<string, QueryError> ResolveParagraph(
        Section section,
        IReadOnlyList<SelectorSegment> segments,
        int segmentIndex,
        SelectorSegment.ParagraphAt paragraphSeg)
    {
        int count = section.Paragraphs.Count;
        if (paragraphSeg.Index > count)
            return Fail(new QueryError.ParagraphOutOfRange(paragraphSeg.Index, count));

        var paragraph = section.Paragraphs[paragraphSeg.Index - 1];

        int nextIndex = segmentIndex + 1;
        if (nextIndex >= segments.Count)
            return Ok(RenderParagraph(paragraph));

        return ExecuteParagraphSegment(paragraph, segments, nextIndex);
    }

    // -------------------------------------------------------------------------
    // .item(N) applied directly to a section (implicit first paragraph that is a list)
    // -------------------------------------------------------------------------

    private static Result<string, QueryError> ResolveItemOnSection(
        Section section,
        IReadOnlyList<SelectorSegment> segments,
        int segmentIndex,
        SelectorSegment.ItemAt itemSeg)
    {
        // When .item(N) is used without a preceding .paragraph(N), we look for
        // the first ListBlock paragraph in the section.
        var listBlock = section.Paragraphs.OfType<Paragraph.ListBlock>().FirstOrDefault();

        if (listBlock is null)
        {
            // If there are paragraphs but none are lists, report NotAList on the first paragraph.
            if (section.Paragraphs.Count > 0)
                return Fail(new QueryError.NotAList());

            return Fail(new QueryError.ItemOutOfRange(itemSeg.Index, 0));
        }

        return ResolveItemOnList(listBlock, segments, segmentIndex, itemSeg);
    }

    // -------------------------------------------------------------------------
    // .item(N) applied to a paragraph context
    // -------------------------------------------------------------------------

    private static Result<string, QueryError> ExecuteParagraphSegment(
        Paragraph paragraph,
        IReadOnlyList<SelectorSegment> segments,
        int segmentIndex)
    {
        var segment = segments[segmentIndex];

        if (segment is not SelectorSegment.ItemAt itemSeg)
            return Ok(RenderParagraph(paragraph));

        if (paragraph is not Paragraph.ListBlock listBlock)
            return Fail(new QueryError.NotAList());

        return ResolveItemOnList(listBlock, segments, segmentIndex, itemSeg);
    }

    private static Result<string, QueryError> ResolveItemOnList(
        Paragraph.ListBlock listBlock,
        IReadOnlyList<SelectorSegment> segments,
        int segmentIndex,
        SelectorSegment.ItemAt itemSeg)
    {
        int count = listBlock.Items.Count;
        if (itemSeg.Index > count)
            return Fail(new QueryError.ItemOutOfRange(itemSeg.Index, count));

        var item = listBlock.Items[itemSeg.Index - 1];

        int nextIndex = segmentIndex + 1;
        if (nextIndex >= segments.Count)
            return Ok(RenderListItem(item));

        return ExecuteListItemSegment(item, segments, nextIndex);
    }

    private static Result<string, QueryError> ExecuteListItemSegment(
        ListItem item,
        IReadOnlyList<SelectorSegment> segments,
        int segmentIndex)
    {
        var segment = segments[segmentIndex];

        if (segment is not SelectorSegment.ItemAt itemSeg)
            return Ok(RenderListItem(item));

        if (item.SubList is null)
            return Fail(new QueryError.ItemOutOfRange(itemSeg.Index, 0));

        return ResolveItemOnList(item.SubList, segments, segmentIndex, itemSeg);
    }

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    private static string RenderDocument(MarkdownDocument document) =>
        RenderSections(document.Sections);

    private static string RenderSections(IReadOnlyList<Section> sections)
    {
        var parts = sections.Select(RenderSection).Where(s => s.Length > 0);
        return string.Join("\n\n", parts);
    }

    private static string RenderSection(Section section)
    {
        var parts = new List<string>();

        if (section.HeadingText is not null)
            parts.Add($"{new string('#', section.HeadingLevel)} {section.HeadingText}");

        foreach (var para in section.Paragraphs)
            parts.Add(RenderParagraph(para));

        foreach (var child in section.Children)
            parts.Add(RenderSection(child));

        return string.Join("\n\n", parts.Where(p => p.Length > 0));
    }

    private static string RenderParagraph(Paragraph paragraph) => paragraph switch
    {
        Paragraph.TextBlock tb  => tb.Content,
        Paragraph.BlockQuote bq => RenderBlockQuote(bq),
        Paragraph.ListBlock lb  => RenderListBlock(lb, indent: 0),
        _                       => string.Empty
    };

    private static string RenderBlockQuote(Paragraph.BlockQuote bq)
    {
        var lines = bq.Content.Split('\n');
        return string.Join("\n", lines.Select(l => $"> {l}"));
    }

    private static string RenderListBlock(Paragraph.ListBlock listBlock, int indent)
    {
        var sb = new StringBuilder();
        string prefix = new string(' ', indent * 2);

        for (int i = 0; i < listBlock.Items.Count; i++)
        {
            if (sb.Length > 0)
                sb.Append('\n');

            var item = listBlock.Items[i];
            string bullet = listBlock.Kind == ListKind.Numbered ? $"{i + 1}." : "-";
            sb.Append($"{prefix}{bullet} {item.Content}");

            if (item.SubList is not null)
            {
                sb.Append('\n');
                sb.Append(RenderListBlock(item.SubList, indent + 1));
            }
        }

        return sb.ToString();
    }

    private static string RenderListItem(ListItem item)
    {
        if (item.SubList is null)
            return item.Content;

        var sb = new StringBuilder();
        sb.Append(item.Content);
        sb.Append('\n');
        sb.Append(RenderListBlock(item.SubList, indent: 0));
        return sb.ToString();
    }

    // -------------------------------------------------------------------------
    // Result helpers
    // -------------------------------------------------------------------------

    private static Result<string, QueryError> Ok(string value) =>
        new Result<string, QueryError>.Ok(value);

    private static Result<string, QueryError> Fail(QueryError error) =>
        new Result<string, QueryError>.Err(error);
}
