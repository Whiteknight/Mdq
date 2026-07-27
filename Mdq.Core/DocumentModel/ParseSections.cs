using System.Diagnostics;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public static partial class MarkdownParser
{
    private static Section ParseTopLevelSection(StringSegment buffer)
    {
        (var startTrivia, buffer) = GatherTrivia(buffer);
        (var paragraphs, buffer) = ParseParagraphs(buffer);
        (var sections, buffer) = ParseSections(buffer, 1);
        (var trailingTrivia, _) = GatherTrivia(buffer);
        return new Section(new Heading(StringSegment.Empty, 0), paragraphs, sections)
        {
            LeadingTrivia = startTrivia,
            TrailingTrivia = trailingTrivia
        };
    }

    private static (Section Section, StringSegment Remainder) ParseSection(StringSegment buffer, int currentLevel)
    {
        (var startTrivia, buffer) = GatherTrivia(buffer);
        (var heading, buffer) = ParseHeading(buffer);
        (var paragraphs, buffer) = ParseParagraphs(buffer);
        (var sections, buffer) = ParseSections(buffer, currentLevel + 1);
        (var trailingTrivia, buffer) = GatherTrivia(buffer);
        var section = new Section(heading, paragraphs, sections)
        {
            LeadingTrivia = startTrivia,
            TrailingTrivia = trailingTrivia
        };
        return (section, buffer);
    }

    private static (int Count, StringSegment Remainder) CountHeadingMarkers(StringSegment buffer)
    {
        // Heading: 1-6 '#', a space, and then the remainder of the text on that line
        // The number of '#' characters indicates the heading level.
        int count = 0;
        while (count < buffer.Length && buffer[count] == '#')
            count++;

        // 7 or more hashes is not a valid heading, by spec
        if (count >= 7)
            return (0, buffer);

        // A '#' must be followed by a space EXCEPT a bare '#' or sequence on a line by itself,
        // which is a valid heading with empty text.
        if (count < buffer.Length && buffer[count] != ' ')
            return (0, buffer);
        return (count, buffer.Subsegment(count));
    }

    private static (Heading Heading, StringSegment Remainder) ParseHeading(StringSegment buffer)
    {
        // NOTE: ParseHeading is only called from ParseSection and leading whitespace is already
        // accounted for by the section. The "LeadingTrivia" here will be the hashes and spaces
        // prior to the heading name.

        // Gather up leading Hashes and leading whitespace for our leading trivia
        (var hashes, _) = CountHeadingMarkers(buffer);
        Debug.Assert(hashes > 0);
        int index = hashes;
        while (index < buffer.Length && char.IsWhiteSpace(buffer[index]))
            index++;
        var leading = buffer.Subsegment(0, index);
        buffer = buffer.Subsegment(index);

        // Read the rest of the line
        (var line, buffer, var _) = ReadLine(buffer, false);

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
        (var trailingTrivia, buffer) = GatherTrivia(buffer);
        var heading = new Heading(line, hashes)
        {
            LeadingTrivia = leading,
            TrailingTrivia = trailingTrivia
        };
        return (heading, buffer);
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

            // Stop parsing sections when we hit a heading of a higher level.
            // (lower number of hashes is a higher level)
            if (section.Heading.Level < currentLevel)
                break;
            sections.Add(section);
            buffer = remainder;
        }

        return (sections, buffer);
    }
}
