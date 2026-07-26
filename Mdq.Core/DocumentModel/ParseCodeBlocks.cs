using System.Text;
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
}
