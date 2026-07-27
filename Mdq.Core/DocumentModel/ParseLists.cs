using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static partial class MarkdownParser
{
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

        // Leading and Trailing trivia should already be accounted for in each individual item
        return (new ListBlock(ListKind.Bulleted, items, paragraphIndex), buffer);
    }

    private static (ListItem Item, StringSegment Remainder) ParseUnorderedListItem(StringSegment buffer, int index)
    {
        var (_, _, leadingTrivia) = GetUnorderedListMarker(buffer);
        buffer = buffer.Subsegment(leadingTrivia.Length);
        (var line, buffer, var lineEnding) = ReadLine(buffer, true);
        var item = new ListItem(line, ListKind.Bulleted, index)
        {
            LeadingTrivia = leadingTrivia,
            TrailingTrivia = lineEnding
        };
        return (item, buffer);
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

        // Leading and trailing trivia are accounted for by each individual list item
        return (new ListBlock(ListKind.Numbered, items, paragraphIndex), buffer);
    }

    private static (ListItem Item, StringSegment Remainder) ParseOrderedListItem(StringSegment buffer, int index)
    {
        var (_, _, leadingTrivia) = GetOrderedListMarker(buffer);
        buffer = buffer.Subsegment(leadingTrivia.Length);
        (var line, buffer, var lineEnding) = ReadLine(buffer, true);
        var item = new ListItem(line, ListKind.Numbered, index)
        {
            LeadingTrivia = leadingTrivia,
            TrailingTrivia = lineEnding
        };
        return (item, buffer);
    }
}
