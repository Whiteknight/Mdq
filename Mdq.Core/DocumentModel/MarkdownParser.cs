using System.Text;
using Mdq.Core.Shared;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static class MarkdownParser
{
    public static Result<MarkdownDocument, MdqError> Parse(string markdown)
    {
        var buffer = new StringSegment(markdown);
        if (IsEntirelyWhitespace(buffer))
            return new MarkdownDocument(new Section(new Heading(StringSegment.Empty, 0), [], [])) { LeadingTrivia = buffer };

        (var startTrivia, buffer) = GatherTrivia(buffer);
        var topLevelSection = ParseTopLevelSection(buffer);
        return new MarkdownDocument(topLevelSection);
    }

    private static bool IsAtEnd(StringSegment buffer)
        => buffer.Length == 0;

    // Gathers whitespace from the current position until the start of the next non-negligible line.
    // Notice that some lines like the start of block quotes may begin with whitespace, so we can't
    // just go until the next non-whitespace character.
    private static (StringSegment Trivia, StringSegment Remainder) GatherTrivia(StringSegment buffer)
    {
        var index = 0;
        bool sawLineBreak = false;
        while (index < buffer.Length)
        {
            var c = buffer[index];
            if (c == '\n' || c == '\r')
            {
                sawLineBreak = true;
                index++;
                continue;
            }

            if (sawLineBreak && c != '\n' && c != '\r')
                break;

            if (!char.IsWhiteSpace(c))
                break;
            index++;
        }

        if (index == 0 || !sawLineBreak)
            return (StringSegment.Empty, buffer);

        var trivia = buffer.Subsegment(0, index);
        return (trivia, buffer.Subsegment(index));
    }

    private static Section ParseTopLevelSection(StringSegment buffer)
    {
        (var paragraphs, buffer) = ParseParagraphs(buffer);
        (var sections, buffer) = ParseSections(buffer, 1);
        var (trailingTrivia, _) = GatherTrivia(buffer);
        return new Section(new Heading(StringSegment.Empty, 0), paragraphs, sections)
        {
            TrailingTrivia = trailingTrivia
        };
    }

    private static (int Count, StringSegment Remainder) CountHeadingMarkers(StringSegment buffer)
    {
        // Heading: One or more '#', a space, and then the remainder of the text on that line
        // The number of '#' characters indicates the heading level.
        int count = 0;
        while (count < buffer.Length && buffer[count] == '#')
            count++;

        // 7 or more hashes is not a valid heading, by spec
        if (count >= 7)
            return (0, buffer);

        // A '#' must be followed by a space EXCEPT a bare '#' or sequence on a line by itself, which is a valid heading with empty text.
        if (count < buffer.Length && buffer[count] != ' ')
            return (0, buffer);
        return (count, buffer.Subsegment(count));
    }

    private static (StringSegment Line, StringSegment Remainder, StringSegment LineEnding) ReadLine(StringSegment buffer, bool includeLineEnding)
    {
        int index = 0;
        while (index < buffer.Length && buffer[index] != '\n' && buffer[index] != '\r')
            index++;
        var line = buffer.Subsegment(0, index);
        var remainder = index < buffer.Length ? buffer.Subsegment(index) : StringSegment.Empty;
        if (!includeLineEnding)
            return (line, remainder, StringSegment.Empty);

        (_, remainder, var newLine) = ReadNewLine(remainder);
        return (line, remainder, newLine);
    }

    private static (bool HasNewLine, StringSegment Remainder, StringSegment NewLine) ReadNewLine(StringSegment buffer)
    {
        if (IsAtEnd(buffer))
            return (false, StringSegment.Empty, buffer);

        if (buffer.Length >= 1 && buffer[0] == '\n')
            return (true, buffer.Subsegment(1), buffer.Subsegment(0, 1));
        if (buffer.Length >= 2 && buffer[0] == '\r' && buffer[1] == '\n')
            return (true, buffer.Subsegment(2), buffer.Subsegment(0, 2));
        if (buffer.Length >= 1 && buffer[0] == '\r')
            return (true, buffer.Subsegment(1), buffer.Subsegment(0, 1));
        return (false, StringSegment.Empty, buffer);
    }

    private static bool IsEntirelyWhitespace(StringSegment buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if (!char.IsWhiteSpace(buffer[i]))
                return false;
        }

        return true;
    }

    private static (List<Paragraph> Paragraphs, StringSegment Remainder) ParseParagraphs(StringSegment buffer)
    {
        var paragraphs = new List<Paragraph>();
        int paragraphIndex = 1;
        while (!IsAtEnd(buffer) && !IsEntirelyWhitespace(buffer))
        {
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

    private static int CountBlockQuoteMarkers(StringSegment buffer)
    {
        int index = 0;
        while (index < buffer.Length && buffer[index] == '>')
            index++;
        return index;
    }

    private static (bool HasBullet, int Indent, StringSegment LeadingTrivia) GetUnorderedListMarker(StringSegment buffer)
    {
        // An unordered line item is an arbitrary indent, followed by exactly one of ('-', '+', '*')
        // followed by one or more spaces.
        int index = 0;
        int indent = 0;
        while (index < buffer.Length && (buffer[index] == ' ' || buffer[index] == '\t'))
        {
            index++;
            indent++;
        }
        if (index >= buffer.Length)
            return (false, 0, StringSegment.Empty);

        if (buffer[index] != '-' && buffer[index] != '*' && buffer[index] != '+')
            return (false, 0, StringSegment.Empty);

        index++;
        if (index >= buffer.Length)
            return (true, indent, buffer.Subsegment(0, index));

        if (buffer[index] != ' ' && buffer[index] != '\n' && buffer[index] != '\r')
            return (false, 0, StringSegment.Empty);

        while (index < buffer.Length && (buffer[index] == ' ' || buffer[index] == '\t'))
            index++;
        return (true, indent, buffer.Subsegment(0, index));
    }

    private static (bool HasNumber, int Indent, StringSegment LeadingTrivia) GetOrderedListMarker(StringSegment buffer)
    {
        int index = 0;
        int indent = 0;
        while (index < buffer.Length && (buffer[index] == ' ' || buffer[index] == '\t'))
        {
            index++;
            indent++;
        }
        if (index >= buffer.Length)
            return (false, 0, StringSegment.Empty);

        int digitsCount = 0;
        while (index < buffer.Length && char.IsDigit(buffer[index]))
        {
            index++;
            digitsCount++;
        }
        if (digitsCount == 0)
            return (false, 0, StringSegment.Empty);

        if (buffer[index] != '.')
            return (false, 0, StringSegment.Empty);
        index++;

        while (index < buffer.Length && (buffer[index] == ' ' || buffer[index] == '\t'))
            index++;
        return (true, indent, buffer.Subsegment(0, index));
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
        // TODO: This only covers the simple textblock case, it doesn't cover lists, block quotes, code blocks, or tables. Those will need to be handled separately.
        Paragraph? paragraph = null;
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

    private static (Paragraph Paragraph, StringSegment Remainder) ParseBlockQuote(StringSegment buffer, int paragraphIndex)
    {
        var totalLength = 0;
        var remainder = buffer;
        var previousTrivia = StringSegment.Empty;
        var sb = new StringBuilder();
        while (!IsAtEnd(remainder))
        {
            (var line, remainder, var trivia) = ReadLine(remainder, true);
            if (CountBlockQuoteMarkers(line) == 0)
                break; // Stop parsing paragraph when we exit the blockquote

            // TODO: We should double-check that we have the same leading trivia here, and that we aren't doing nested blockquotes
            int index = 0;
            while (index < line.Length && line[index] == '>')
                index++;
            while (index < line.Length && char.IsWhiteSpace(line[index]))
                index++;

            var leading = line.Subsegment(0, index);
            line = line.Subsegment(index);
            sb.Append(line.AsSpan());

            totalLength += previousTrivia.Length + leading.Length + line.Length;
            previousTrivia = trivia;
        }

        // TODO: We should keep track of each individual line with it's leading trivia here, so we can
        // faithfully round-trip reassemble it.
        return (new BlockQuote(sb.ToString(), paragraphIndex) { TrailingTrivia = previousTrivia }, buffer.Subsegment(totalLength + previousTrivia.Length));
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

    private static (ListBlock Paragraph, StringSegment Remainder) ParseUnorderedList(StringSegment buffer, int paragraphIndex, int indent)
    {
        var items = new List<ListItem>();
        int count = 0;
        while (!IsAtEnd(buffer))
        {
            var (hasBullet, thisLineIndent, _) = GetUnorderedListMarker(buffer);
            if (hasBullet)
            {
                // Stop parsing paragraph when we hit a list item with less indentation.
                if (thisLineIndent < indent)
                    break;
                if (thisLineIndent > indent)
                {
                    (var sublist, buffer) = ParseUnorderedList(buffer, paragraphIndex, thisLineIndent);
                    items[^1] = items[^1] with { SubList = sublist };
                    continue;
                }

                (var item, buffer) = ParseUnorderedListItem(buffer, ++count);
                items.Add(item);
                continue;
            }

            (var hasOrderedList, thisLineIndent, _) = GetOrderedListMarker(buffer);
            if (hasOrderedList && thisLineIndent > indent)
            {
                (var sublist, buffer) = ParseOrderedList(buffer, paragraphIndex, thisLineIndent);
                items[^1] = items[^1] with { SubList = sublist };
                continue;
            }

            break;
        }
        return (new ListBlock(ListKind.Bulleted, items, paragraphIndex), buffer);
    }

    private static (ListItem Item, StringSegment Remainder) ParseUnorderedListItem(StringSegment buffer, int index)
    {
        var (hasBullet, indent, leadingTrivia) = GetUnorderedListMarker(buffer);
        buffer = buffer.Subsegment(leadingTrivia.Length);
        (var line, buffer, var lineEnding) = ReadLine(buffer, true);
        return (new ListItem(line, ListKind.Bulleted, index) { LeadingTrivia = leadingTrivia, TrailingTrivia = lineEnding }, buffer);
    }

    private static (ListBlock Paragraph, StringSegment Remainder) ParseOrderedList(StringSegment buffer, int paragraphIndex, int indent)
    {
        var items = new List<ListItem>();
        int count = 0;
        while (!IsAtEnd(buffer))
        {
            var (hasOrderedList, thisLineIndent, _) = GetOrderedListMarker(buffer);
            if (hasOrderedList)
            {
                // Stop parsing paragraph when we hit a list item with less indentation.
                if (thisLineIndent < indent)
                    break;

                // If we have a higher indent than previous lines, we're starting a new sublist
                if (thisLineIndent > indent)
                {
                    (var sublist, buffer) = ParseOrderedList(buffer, paragraphIndex, thisLineIndent);
                    items[^1] = items[^1] with { SubList = sublist };
                    continue;
                }

                (var item, buffer) = ParseOrderedListItem(buffer, ++count);
                items.Add(item);
                continue;
            }

            // We're starting an unordered list. Check to see if it's a sublist or not
            (var hasBullet, thisLineIndent, _) = GetUnorderedListMarker(buffer);
            if (hasBullet && thisLineIndent > indent)
            {
                (var sublist, buffer) = ParseUnorderedList(buffer, paragraphIndex, thisLineIndent);
                items[^1] = items[^1] with { SubList = sublist };
                continue;
            }

            break;
        }
        return (new ListBlock(ListKind.Numbered, items, paragraphIndex), buffer);
    }

    private static (ListItem Item, StringSegment Remainder) ParseOrderedListItem(StringSegment buffer, int index)
    {
        var (_, _, leadingTrivia) = GetOrderedListMarker(buffer);
        buffer = buffer.Subsegment(leadingTrivia.Length);
        (var line, buffer, var lineEnding) = ReadLine(buffer, true);
        return (new ListItem(line, ListKind.Numbered, index) { LeadingTrivia = leadingTrivia, TrailingTrivia = lineEnding }, buffer);
    }

    private static (bool IsCodeBlock, StringSegment LeadingTrivia, StringSegment Remainder) GetIndentedCodeBlockStart(StringSegment buffer)
    {
        int index = 0;
        while (index < buffer.Length && (buffer[index] == ' '))
            index++;
        if (index < 4)
            return (false, StringSegment.Empty, buffer);
        var leadingTrivia = buffer.Subsegment(0, index);
        var remainder = buffer.Subsegment(index);
        return (true, leadingTrivia, remainder);
    }

    private static (Paragraph Paragraph, StringSegment Remainder) ParseIndentedCodeBlock(StringSegment buffer, int paragraphIndex)
    {
        (_, var firstLineIndent, _) = GetIndentedCodeBlockStart(buffer);

        var sb = new StringBuilder();
        StringSegment current = buffer;
        while (!IsAtEnd(current))
        {
            (var line, var nextCurrent, var lineEnding) = ReadLine(current, true);

            // Stop parsing at blank or short line
            if (line.Length == 0 || line.Length < firstLineIndent.Length)
                break;

            // Stop parsing if the indent is wrong
            var lineIndent = line.Subsegment(0, firstLineIndent.Length);
            if (!lineIndent.Equals(firstLineIndent, StringComparison.Ordinal))
                break;

            sb.Append(line.Subsegment(firstLineIndent.Length).AsSpan());
            sb.Append(lineEnding.AsSpan());
            current = nextCurrent;
        }

        buffer = current;
        return (new CodeBlock(StringSegment.Empty, sb.ToString(), paragraphIndex) { LeadingTrivia = firstLineIndent }, buffer);
    }

    private static (bool IsCodeBlock, StringSegment Marker, StringSegment Language, StringSegment TrailingTrivia, StringSegment Remainder) GetFencedCodeBlockStart(StringSegment buffer)
    {
        if (!buffer.StartsWith("```", StringComparison.Ordinal))
            return (false, StringSegment.Empty, StringSegment.Empty, StringSegment.Empty, buffer);

        int index = 3;
        while (index < buffer.Length && !char.IsWhiteSpace(buffer[index]))
            index++;
        var marker = buffer.Subsegment(0, 3);
        var language = buffer.Subsegment(3, index - 3);
        var (trailing, remainder) = GatherTrivia(buffer.Subsegment(index));
        return (true, marker, language, trailing, remainder);
    }

    private static (Paragraph Paragraph, StringSegment Remainder) ParseFencedCodeBlock(StringSegment buffer, int paragraphIndex)
    {
        (_, var marker, var language, var trailingTrivia, buffer) = GetFencedCodeBlockStart(buffer);
        int index = 0;
        bool foundCloseFence = false;
        StringSegment closeFence;
        StringSegment current = buffer;
        while (!IsAtEnd(current))
        {
            (var line, current, var lineEnding) = ReadLine(current, true);
            if (line.Equals("```", StringComparison.Ordinal))
            {
                foundCloseFence = true;
                closeFence = line.Subsegment(0, 3);
                break;
            }

            index += line.Length + lineEnding.Length;
        }

        var contents = buffer.Subsegment(0, index);
        buffer = current;

        if (!foundCloseFence)
            return (new CodeBlock(language, contents, paragraphIndex) { LeadingTrivia = marker }, StringSegment.Empty);

        (trailingTrivia, var remainder) = GatherTrivia(buffer);
        // TODO: Being kind of sloppy here about keeping track of all the various bits of trivia and markers.
        return (new CodeBlock(language, contents, paragraphIndex) { LeadingTrivia = marker, TrailingTrivia = trailingTrivia }, remainder);
    }

    private static (bool IsPipeTable, StringSegment Remainder) IsPipeTable(StringSegment buffer)
    {
        return buffer.StartsWith("|", StringComparison.Ordinal)
            ? (true, buffer)
            : (false, buffer);
    }

    private static bool IsAllDashes(StringSegment segment)
    {
        for (int i = 0; i < segment.Length; i++)
        {
            var c = segment[i];
            if (c != '-' && !char.IsWhiteSpace(c))
                return false;
        }
        return true;
    }

    private static (Paragraph Paragraph, StringSegment Remainder) ParsePipeTable(StringSegment buffer, int paragraphIndex)
    {
        (var line, buffer, var lineEnding) = ReadLine(buffer, true);
        var rawHeaderCells = line.Split(['|']).Select(cell => cell.Trim()).ToList();
        var headerCells = rawHeaderCells.Skip(1).Take(rawHeaderCells.Count - 2).Select((c, i) => new TableCell(c, i + 1)).ToList();
        var header = new TableRow(headerCells, 0) { TrailingTrivia = lineEnding };

        var rows = new List<TableRow>();
        int count = 0;
        while (true)
        {
            (line, buffer, lineEnding) = ReadLine(buffer, true);
            if (line.Length == 0)
                break; // Stop parsing paragraph when we hit a blank line.
            if (!line.StartsWith("|", StringComparison.Ordinal))
                break; // Stop parsing paragraph when we hit a line that doesn't start with a pipe.

            var rawCells = line.Split(['|']).Select(cell => cell.Trim()).ToList();
            if (rawCells.All(IsAllDashes))
                continue; // Skip the separator line.
            var cells = rawCells.Skip(1).Take(rawCells.Count - 2).Select((c, i) => new TableCell(c, i + 1)).ToList();

            var row = new TableRow(cells, ++count) { TrailingTrivia = lineEnding };
            rows.Add(row);
        }

        return (new TableBlock(header, rows, paragraphIndex) { }, buffer);
    }

    private static (Heading Heading, StringSegment Remainder) ParseHeading(StringSegment buffer)
    {
        (var hashes, _) = CountHeadingMarkers(buffer);
        if (hashes == 0)
            return (new Heading(StringSegment.Empty, 0), buffer);

        int index = hashes;
        while (index < buffer.Length && char.IsWhiteSpace(buffer[index]))
            index++;

        var leading = buffer.Subsegment(0, index);
        var remainder = buffer.Subsegment(index);

        (var line, remainder, var _) = ReadLine(remainder, false);

        // Trim trailing whitespace and '#' characters
        // '## HEADING ##' is the same as '## HEADING', so we strip off the trailing hashes.
        // I do not confirm that the leading and trailing hashes match.
        // HOWEVER note that '## Heading#' does not strip the trailing hash. it must be whitespace-hash-whitespace to strip
        int rIndex = line.Length - 1;
        while (rIndex >= 0 && char.IsWhiteSpace(line[rIndex]))
            rIndex--;
        // Only strip trailing '#' if they are preceded by whitespace (or the text is only '#').
        // CommonMark: '## foo ##' strips, but '## foo#' does not.
        int hashEnd = rIndex;
        while (hashEnd >= 0 && line[hashEnd] == '#')
            hashEnd--;
        if (hashEnd < 0 || char.IsWhiteSpace(line[hashEnd]))
        {
            rIndex = hashEnd;
            while (rIndex >= 0 && char.IsWhiteSpace(line[rIndex]))
                rIndex--;
        }
        line = line.Subsegment(0, rIndex + 1);
        // TODO: Should we include trailing hashes in the trailing trivia?
        (var trailingTrivia, remainder) = GatherTrivia(remainder);
        return (new Heading(line, hashes) { LeadingTrivia = leading, TrailingTrivia = trailingTrivia }, remainder);
    }

    private static (Section Section, StringSegment Remainder) ParseSection(StringSegment buffer, int currentLevel)
    {
        (var heading, buffer) = ParseHeading(buffer);
        (var paragraphs, buffer) = ParseParagraphs(buffer);
        (var sections, buffer) = ParseSections(buffer, currentLevel + 1);
        var (trailingTrivia, remainder) = GatherTrivia(buffer);
        var section = new Section(heading, paragraphs, sections)
        {
            TrailingTrivia = trailingTrivia
        };
        return (section, remainder);
    }

    private static (List<Section> Sections, StringSegment Remainder) ParseSections(StringSegment buffer, int currentLevel)
    {
        var sections = new List<Section>();
        while (!IsAtEnd(buffer) && !IsEntirelyWhitespace(buffer))
        {
            var (markers, _) = CountHeadingMarkers(buffer);
            if (markers == 0 || markers < currentLevel)
                return (sections, buffer);

            var (section, remainder) = ParseSection(buffer, currentLevel);
            if (section.Heading.Level < currentLevel)
                break; // Stop parsing sections when we hit a heading of a higher level.
            sections.Add(section);
            buffer = remainder;
        }

        return (sections, buffer);
    }
}
