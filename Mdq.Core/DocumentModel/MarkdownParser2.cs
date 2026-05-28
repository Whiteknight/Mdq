using Microsoft.Extensions.Primitives;
using Mdq.Core.Shared;

namespace Mdq.Core.DocumentModel;

/// <summary>
/// A custom structural Markdown parser that preserves source text via StringSegment references.
/// Does not depend on Markdig. Currently handles ATX headings and text paragraphs.
/// </summary>
public static class MarkdownParser2
{
    public static Result<MarkdownDocument, MdqError> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return new MarkdownDocument([]);

        var blocks = RecognizeBlocks(markdown);
        var flatSections = GroupIntoSections(blocks);
        var tree = BuildSectionTree(flatSections);
        return new MarkdownDocument(tree.ToList());
    }

    // -------------------------------------------------------------------------
    // Phase 1: Recognize blocks from source lines
    // -------------------------------------------------------------------------

    private enum BlockKind { Heading, Paragraph, BlankLine }

    private readonly record struct Block(
        BlockKind Kind,
        int HeadingLevel,
        StringSegment Content,        // The meaningful content (heading text or paragraph text)
        StringSegment LeadingTrivia,   // Whitespace before this block
        StringSegment TrailingTrivia   // Trailing whitespace (newline at end of block)
    );

    private static List<Block> RecognizeBlocks(string source)
    {
        var blocks = new List<Block>();
        var lines = ScanLines(source);
        int i = 0;

        while (i < lines.Count)
        {
            // Accumulate leading blank lines as trivia for the next block
            int triviaStart = lines[i].Start;
            while (i < lines.Count && IsBlankLine(source, lines[i]))
                i++;

            if (i >= lines.Count)
                break;

            var leadingTrivia = new StringSegment(source, triviaStart, lines[i].Start - triviaStart);

            // Check if current line is an ATX heading
            var line = lines[i];
            int headingLevel = GetAtxHeadingLevel(source, line);

            if (headingLevel > 0)
            {
                // ATX heading: single line
                var contentRange = GetHeadingContentRange(source, line, headingLevel);
                var content = new StringSegment(source, contentRange.Start, contentRange.Length);
                var trailing = GetLineTrailingTrivia(source, line);
                blocks.Add(new Block(BlockKind.Heading, headingLevel, content, leadingTrivia, trailing));
                i++;
            }
            else
            {
                // Paragraph: consecutive non-blank, non-heading lines
                int paraStart = line.Start;
                int paraEnd = line.Start + line.Length;
                i++;

                while (i < lines.Count && !IsBlankLine(source, lines[i]) && GetAtxHeadingLevel(source, lines[i]) == 0)
                {
                    paraEnd = lines[i].Start + lines[i].Length;
                    i++;
                }

                var content = new StringSegment(source, paraStart, paraEnd - paraStart);
                // Trailing trivia for paragraph: the newline(s) at the end of the last line
                var trailingStart = paraEnd;
                var trailingEnd = i < lines.Count ? lines[i].Start : source.Length;
                // Only include the immediate line ending, not subsequent blank lines
                var trailing = trailingStart < source.Length
                    ? new StringSegment(source, trailingStart, GetLineEndingLength(source, trailingStart))
                    : StringSegment.Empty;

                blocks.Add(new Block(BlockKind.Paragraph, 0, content, leadingTrivia, trailing));
            }
        }

        return blocks;
    }

    // -------------------------------------------------------------------------
    // Phase 2: Group blocks into flat sections
    // -------------------------------------------------------------------------

    private sealed record FlatSection(
        StringSegment HeadingText,
        int HeadingLevel,
        StringSegment HeadingLeadingTrivia,
        StringSegment HeadingTrailingTrivia,
        List<Paragraph> Paragraphs);

    private static List<FlatSection> GroupIntoSections(List<Block> blocks)
    {
        var sections = new List<FlatSection>();
        var current = new FlatSection(default, 0, default, default, []);
        int paragraphIndex = 1;

        foreach (var block in blocks)
        {
            if (block.Kind == BlockKind.Heading)
            {
                sections.Add(current);
                current = new FlatSection(block.Content, block.HeadingLevel, block.LeadingTrivia, block.TrailingTrivia, []);
                paragraphIndex = 1;
            }
            else if (block.Kind == BlockKind.Paragraph)
            {
                var textBlock = new TextBlock(block.Content, paragraphIndex)
                {
                    LeadingTrivia = block.LeadingTrivia,
                    TrailingTrivia = block.TrailingTrivia
                };
                current.Paragraphs.Add(textBlock);
                paragraphIndex++;
            }
        }

        sections.Add(current);
        return sections;
    }

    // -------------------------------------------------------------------------
    // Phase 3: Build section tree (same algorithm as MarkdownParser)
    // -------------------------------------------------------------------------

    private static IReadOnlyList<Section> BuildSectionTree(List<FlatSection> flat)
    {
        var rootChildren = new List<Section>();
        var stack = new Stack<(FlatSection Flat, List<Section> Children)>();
        var sentinel = new FlatSection(default, 0, default, default, []);
        stack.Push((sentinel, rootChildren));

        foreach (var section in flat)
        {
            if (section.HeadingLevel == 0)
            {
                if (section.Paragraphs.Count > 0)
                    rootChildren.Add(ToSection(section, []));
                continue;
            }

            while (stack.Count > 1 && stack.Peek().Flat.HeadingLevel >= section.HeadingLevel)
            {
                var (popped, poppedChildren) = stack.Pop();
                var built = ToSection(popped, poppedChildren);
                stack.Peek().Children.Add(built);
            }

            var children = new List<Section>();
            stack.Push((section, children));
        }

        while (stack.Count > 1)
        {
            var (popped, poppedChildren) = stack.Pop();
            var built = ToSection(popped, poppedChildren);
            stack.Peek().Children.Add(built);
        }

        return rootChildren;
    }

    private static Section ToSection(FlatSection flat, List<Section> children)
    {
        var heading = new Heading(flat.HeadingText, flat.HeadingLevel)
        {
            LeadingTrivia = flat.HeadingLeadingTrivia,
            TrailingTrivia = flat.HeadingTrailingTrivia
        };
        return new Section(heading, flat.Paragraphs, children);
    }

    // -------------------------------------------------------------------------
    // Line scanning helpers
    // -------------------------------------------------------------------------

    private readonly record struct LineRange(int Start, int Length);

    private static List<LineRange> ScanLines(string source)
    {
        var lines = new List<LineRange>();
        int pos = 0;

        while (pos < source.Length)
        {
            int lineStart = pos;
            // Find end of line content (before \r\n or \n)
            while (pos < source.Length && source[pos] != '\r' && source[pos] != '\n')
                pos++;

            int lineEnd = pos;
            lines.Add(new LineRange(lineStart, lineEnd - lineStart));

            // Skip line ending
            if (pos < source.Length && source[pos] == '\r')
                pos++;
            if (pos < source.Length && source[pos] == '\n')
                pos++;
        }

        return lines;
    }

    private static bool IsBlankLine(string source, LineRange line)
    {
        for (int i = line.Start; i < line.Start + line.Length; i++)
        {
            if (source[i] != ' ' && source[i] != '\t')
                return false;
        }
        return true;
    }

    private static int GetAtxHeadingLevel(string source, LineRange line)
    {
        int pos = line.Start;
        int end = line.Start + line.Length;

        // Skip leading spaces (up to 3)
        int spaces = 0;
        while (pos < end && source[pos] == ' ' && spaces < 3)
        {
            pos++;
            spaces++;
        }

        // Count # characters
        int level = 0;
        while (pos < end && source[pos] == '#' && level < 7)
        {
            pos++;
            level++;
        }

        if (level == 0 || level > 6)
            return 0;

        // Must be followed by space or end of line
        if (pos < end && source[pos] != ' ' && source[pos] != '\t')
            return 0;

        return level;
    }

    private static LineRange GetHeadingContentRange(string source, LineRange line, int headingLevel)
    {
        int pos = line.Start;
        int end = line.Start + line.Length;

        // Skip leading spaces
        while (pos < end && source[pos] == ' ')
            pos++;

        // Skip # characters
        pos += headingLevel;

        // Skip space after #
        if (pos < end && (source[pos] == ' ' || source[pos] == '\t'))
            pos++;

        // Trim trailing # and spaces (optional closing sequence)
        int contentEnd = end;
        while (contentEnd > pos && source[contentEnd - 1] == ' ')
            contentEnd--;
        while (contentEnd > pos && source[contentEnd - 1] == '#')
            contentEnd--;
        // If we trimmed trailing #, also trim the space before them
        if (contentEnd < end && contentEnd > pos && source[contentEnd - 1] == ' ')
            contentEnd--;

        return new LineRange(pos, contentEnd - pos);
    }

    private static StringSegment GetLineTrailingTrivia(string source, LineRange line)
    {
        int pos = line.Start + line.Length;
        int len = GetLineEndingLength(source, pos);
        return len > 0 ? new StringSegment(source, pos, len) : StringSegment.Empty;
    }

    private static int GetLineEndingLength(string source, int pos)
    {
        if (pos >= source.Length)
            return 0;
        if (source[pos] == '\r')
            return (pos + 1 < source.Length && source[pos + 1] == '\n') ? 2 : 1;
        if (source[pos] == '\n')
            return 1;
        return 0;
    }
}
