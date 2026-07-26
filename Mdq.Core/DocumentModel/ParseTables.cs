using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static partial class MarkdownParser
{
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
}
