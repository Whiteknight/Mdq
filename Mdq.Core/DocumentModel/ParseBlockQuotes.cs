using System.Text;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static partial class MarkdownParser
{
    private static int CountBlockQuoteMarkers(StringSegment buffer)
    {
        int index = 0;
        while (index < buffer.Length && buffer[index] == '>')
            index++;
        return index;
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
}
