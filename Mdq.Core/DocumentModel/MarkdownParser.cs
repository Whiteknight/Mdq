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
        int count = 0;
        while (count < buffer.Length && buffer[count] == '#')
            count++;
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

        if (index + 1 < buffer.Length && buffer[index] == '\n')
            return (line, remainder.Subsegment(1), remainder.Subsegment(0, 1));
        if (index + 2 < buffer.Length && buffer[index] == '\r' && buffer[index + 1] == '\n')
            return (line, remainder.Subsegment(2), remainder.Subsegment(0, 2));
        return (line, remainder, StringSegment.Empty);
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
        while (!IsAtEnd(buffer) || !IsEntirelyWhitespace(buffer))
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

    private static (int BulletCount, int Indent, StringSegment LeadingTrivia) GetUnorderedListMarker(StringSegment buffer)
    {
        int index = 0;
        int indent = 0;
        while (index < buffer.Length && (buffer[index] == ' ' || buffer[index] == '\t'))
        {
            index++;
            indent++;
        }
        if (index >= buffer.Length)
            return (0, 0, StringSegment.Empty);

        int bulletCount = 0;
        while (index < buffer.Length && (buffer[index] == '-' || buffer[index] == '*' || buffer[index] == '+'))
        {
            index++;
            bulletCount++;
        }
        if (bulletCount == 0)
            return (0, 0, StringSegment.Empty);

        while (index < buffer.Length && (buffer[index] == ' ' || buffer[index] == '\t'))
            index++;
        return (bulletCount, indent, buffer.Subsegment(0, index));
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

    private static (Paragraph Paragraph, StringSegment Remainder) ParseParagraph(StringSegment buffer, int paragraphIndex)
    {
        // TODO: This only covers the simple textblock case, it doesn't cover lists, block quotes, code blocks, or tables. Those will need to be handled separately.
        Paragraph? paragraph = null;
        StringSegment remainder;

        // TODO: UnorderedList, OrderedList, CodeBlock, Table.
        // TODO: Ordered and Unordered lists are identified by their indentation, so a list starting with different indentation is a new list.
        if (CountBlockQuoteMarkers(buffer) > 0)
        {
            (paragraph, remainder) = ParseBlockQuote(buffer, paragraphIndex);
            (var trailing, remainder) = GatherTrivia(remainder);
            return (paragraph with { TrailingTrivia = trailing }, remainder);
        }

        var (bullets, indents, _) = GetUnorderedListMarker(buffer);
        if (bullets > 0)
        {
            (paragraph, remainder) = ParseUnorderedList(buffer, paragraphIndex, indents, bullets);
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

        var (hasIndentedCodeBlock, _, _) = GetIndentedCodeBlockStart(buffer);
        if (hasIndentedCodeBlock)
        {
            (paragraph, remainder) = ParseIndentedCodeBlock(buffer, paragraphIndex);
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
            if (IsEntirelyWhitespace(line))
                break; // Stop parsing paragraph when we hit a blank line.
            if (CountHeadingMarkers(line).Count > 0)
                break; // Stop parsing paragraph when we hit a heading.

            totalLength += line.Length + previousTrivia.Length;
            previousTrivia = trivia;
        }
        var totalSegment = buffer.Subsegment(0, totalLength);
        buffer = buffer.Subsegment(totalLength);
        return (new TextBlock(totalSegment, paragraphIndex), buffer);
    }

    private static (ListBlock Paragraph, StringSegment Remainder) ParseUnorderedList(StringSegment buffer, int paragraphIndex, int indent, int markerCount)
    {
        var items = new List<ListItem>();
        int count = 0;
        while (!IsAtEnd(buffer))
        {
            var (mc, ind, _) = GetUnorderedListMarker(buffer);
            if (mc != markerCount)
                break;
            if (ind < indent)
                break; // Stop parsing paragraph when we hit a list item with less indentation.
            if (ind > indent)
            {
                (var sublist, buffer) = ParseUnorderedList(buffer, paragraphIndex, ind, mc);
                items[^1] = items[^1] with { SubList = sublist };
                continue;
            }
            // TODO: Also need to parse nested OrderedList inside this unordered list item.

            (var item, buffer) = ParseUnorderedListItem(buffer, mc, ++count);
            items.Add(item);
        }
        return (new ListBlock(ListKind.Bulleted, items, paragraphIndex), buffer);
    }

    private static (ListItem Item, StringSegment Remainder) ParseUnorderedListItem(StringSegment buffer, int level, int index)
    {
        var (count, indent, leadingTrivia) = GetUnorderedListMarker(buffer);
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
            var (ok, ind, _) = GetOrderedListMarker(buffer);
            if (!ok)
                break;
            if (ind < indent)
                break; // Stop parsing paragraph when we hit a list item with less indentation.
            if (ind > indent)
            {
                (var sublist, buffer) = ParseOrderedList(buffer, paragraphIndex, ind);
                items[^1] = items[^1] with { SubList = sublist };
                continue;
            }
            // TODO: Need to handle nested unordered list inside ordered list item.

            (var item, buffer) = ParseOrderedListItem(buffer, ++count);
            items.Add(item);
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
        while (index < buffer.Length && (buffer[index] == ' ' || buffer[index] == '\t'))
            index++;
        if (index == 0)
            return (false, StringSegment.Empty, buffer);
        var leadingTrivia = buffer.Subsegment(0, index);
        var remainder = buffer.Subsegment(index);
        return (true, leadingTrivia, remainder);
    }

    private static (Paragraph Paragraph, StringSegment Remainder) ParseIndentedCodeBlock(StringSegment buffer, int paragraphIndex)
    {
        (_, var firstLineIndent, _) = GetIndentedCodeBlockStart(buffer);

        var sb = new StringBuilder();
        while (!IsAtEnd(buffer))
        {
            (var line, buffer, var lineEnding) = ReadLine(buffer, true);
            if (line.Length == 0)
                break; // Stop parsing paragraph when we hit a blank line.

            var lineIndent = line.Subsegment(0, firstLineIndent.Length);
            if (!lineIndent.Equals(firstLineIndent, StringComparison.Ordinal))
                break; // Stop parsing paragraph when we hit a line that doesn't have the same leading trivia.

            sb.Append(line.Subsegment(firstLineIndent.Length).AsSpan());
            sb.Append(lineEnding.AsSpan());
        }
        return (new CodeBlock(StringSegment.Empty, sb.ToString(), paragraphIndex) { LeadingTrivia = firstLineIndent }, buffer);
    }

    private static (bool IsCodeBlock, StringSegment Marker, StringSegment Language, StringSegment TrailingTrivia, StringSegment Remainder) GetFencedCodeBlockStart(StringSegment buffer)
    {
        if (!buffer.StartsWith("```", StringComparison.OrdinalIgnoreCase))
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
        while (index < buffer.Length - 3 && !(buffer[index] == '`' && buffer[index + 1] == '`' && buffer[index + 2] == '`'))
            index++;

        var contents = buffer.Subsegment(0, index);
        buffer = buffer.Subsegment(index);
        var ending = buffer.Subsegment(0, 3);
        buffer = buffer.Subsegment(3);
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
        var headerCells = line.Split(['|']).Select(cell => cell.Trim()).ToList();
        var header = new TableRow(headerCells.Skip(1).Take(headerCells.Count - 2).ToList(), 0) { TrailingTrivia = lineEnding };

        var rows = new List<TableRow>();
        int count = 0;
        while (true)
        {
            (line, buffer, lineEnding) = ReadLine(buffer, true);
            if (line.Length == 0)
                break; // Stop parsing paragraph when we hit a blank line.
            if (!line.StartsWith("|", StringComparison.Ordinal))
                break; // Stop parsing paragraph when we hit a line that doesn't start with a pipe.

            var cells = line.Split(['|']).Select(cell => cell.Trim()).ToList();
            cells = cells.Skip(1).Take(cells.Count - 2).ToList();
            if (cells.All(IsAllDashes))
                continue; // Skip the separator line.

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
        while (!IsAtEnd(buffer) || !IsEntirelyWhitespace(buffer))
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
