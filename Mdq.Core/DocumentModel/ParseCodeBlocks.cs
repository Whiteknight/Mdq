using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static partial class MarkdownParser
{
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

        var lines = new List<StringSegment>();
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

            lines.Add(line.Subsegment(firstLineIndent.Length));
            current = nextCurrent;
        }

        var cb = new IndentedCodeBlock(lines, paragraphIndex, firstLineIndent)
        {
            LeadingTrivia = firstLineIndent
        };
        return (cb, current);
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
        (_, var marker, var language, _, buffer) = GetFencedCodeBlockStart(buffer);
        var lines = new List<StringSegment>();
        bool foundCloseFence = false;
        StringSegment closeFence = StringSegment.Empty;
        while (!IsAtEnd(buffer))
        {
            (var line, buffer, var lineEnding) = ReadLine(buffer, true);
            if (line.Equals("```", StringComparison.Ordinal))
            {
                foundCloseFence = true;
                closeFence = line.Subsegment(0, 3);
                break;
            }

            lines.Add(line);
        }

        if (!foundCloseFence)
        {
            // TODO: Being kind of sloppy here about keeping track of all the various bits of trivia and markers.
            var cb = new FencedCodeBlock(language, lines, paragraphIndex)
            {
                LeadingTrivia = marker,
                ClosingFence = closeFence
            };
            return (cb, StringSegment.Empty);
        }

        (var trailingTrivia, var remainder) = GatherTrivia(buffer);
        // TODO: Being kind of sloppy here about keeping track of all the various bits of trivia and markers.
        var cbi = new FencedCodeBlock(language, lines, paragraphIndex)
        {
            LeadingTrivia = marker,
            ClosingFence = closeFence,
            TrailingTrivia = trailingTrivia
        };
        return (cbi, remainder);
    }
}
