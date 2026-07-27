using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static partial class MarkdownParser
{
    private static (List<Paragraph> Paragraphs, StringSegment Remainder) ParseParagraphs(StringSegment buffer)
    {
        var paragraphs = new List<Paragraph>();
        int paragraphIndex = 1;
        while (!IsAtEnd(buffer) && !IsEntirelyWhitespace(buffer))
        {
            // Read a line. If it's empty we can advance
            // If it's a heading, we break from here and will start a new section.
            // If the line is non-trivial we will parse the paragraph.
            // "line" and "remainder" here are discarded and we just continue parsing the buffer.
            var (line, remainder, _) = ReadLine(buffer, true);
            if (line.Length == 0)
            {
                buffer = remainder;
                continue;
            }

            (var hashes, _) = CountHeadingMarkers(buffer);
            if (hashes > 0)
                break; // Stop parsing paragraphs when we hit a heading.

            (var paragraph, buffer) = ParseParagraph(buffer, paragraphIndex++);
            paragraphs.Add(paragraph);
        }
        return (paragraphs, buffer);
    }

    private static bool StartsNewBlock(StringSegment line)
        => CountHeadingMarkers(line).Count > 0
        || CountBlockQuoteMarkers(line) > 0
        || GetUnorderedListMarker(line).HasBullet
        || GetOrderedListMarker(line).HasNumber
        || GetFencedCodeBlockStart(line).IsCodeBlock
        || IsPipeTable(line).IsPipeTable;

    private static (Paragraph Paragraph, StringSegment Remainder) ParseParagraph(StringSegment buffer, int paragraphIndex)
    {
        Paragraph? paragraph;
        StringSegment remainder;

        var (hasIndentedCodeBlock, _, _) = GetIndentedCodeBlockStart(buffer);
        if (hasIndentedCodeBlock)
        {
            (paragraph, remainder) = ParseIndentedCodeBlock(buffer, paragraphIndex);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        if (!StartsNewBlock(buffer))
        {
            (paragraph, remainder) = ParseTextBlock(buffer, paragraphIndex);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        if (CountBlockQuoteMarkers(buffer) > 0)
        {
            (paragraph, remainder) = ParseBlockQuote(buffer, paragraphIndex);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        var (hasBullet, indents, _) = GetUnorderedListMarker(buffer);
        if (hasBullet)
        {
            (paragraph, remainder) = ParseUnorderedList(buffer, paragraphIndex, indents);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        (var hasNumberedList, indents, _) = GetOrderedListMarker(buffer);
        if (hasNumberedList)
        {
            (paragraph, remainder) = ParseOrderedList(buffer, paragraphIndex, indents);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        var (hasFencedCodeBlock, _, _, _, _) = GetFencedCodeBlockStart(buffer);
        if (hasFencedCodeBlock)
        {
            (paragraph, remainder) = ParseFencedCodeBlock(buffer, paragraphIndex);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        var (isPipeTable, _) = IsPipeTable(buffer);
        if (isPipeTable)
        {
            (paragraph, remainder) = ParsePipeTable(buffer, paragraphIndex);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        (paragraph, remainder) = ParseTextBlock(buffer, paragraphIndex);
        (var trailingTrivia, remainder) = GatherTrivia(remainder);
        return (paragraph with { TrailingTrivia = trailingTrivia }, remainder);
    }

    private static (Paragraph Paragraph, StringSegment Remainder) ParseTextBlock(StringSegment buffer, int paragraphIndex)
    {
        var totalLength = 0;
        var remainder = buffer;
        var previousTrivia = StringSegment.Empty;
        while (!IsAtEnd(remainder))
        {
            (var line, remainder, var trivia) = ReadLine(remainder, true);

            // Stop parsing paragraph when we hit a blank line or when we've started some new type of block
            if (IsEntirelyWhitespace(line) || StartsNewBlock(line))
                break;

            totalLength += line.Length + previousTrivia.Length;
            previousTrivia = trivia;
        }
        var totalSegment = buffer.Subsegment(0, totalLength);
        buffer = buffer.Subsegment(totalLength);
        return (new TextBlock(totalSegment, paragraphIndex), buffer);
    }
}
