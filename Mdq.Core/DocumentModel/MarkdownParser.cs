using Mdq.Core.Shared;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static partial class MarkdownParser
{
    public static Result<MarkdownDocument, MdqError> Parse(string markdown)
    {
        var buffer = new StringSegment(markdown);
        if (IsEntirelyWhitespace(buffer))
            return MarkdownDocument.Empty(buffer);

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
}
