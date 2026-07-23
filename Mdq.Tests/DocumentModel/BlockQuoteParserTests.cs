using AwesomeAssertions;
using Mdq.Core.Shared;
using DM = Mdq.Core.DocumentModel;

namespace Mdq.Tests.DocumentModel;

/// <summary>
/// Tests for block quote parsing: content, multi-line, nesting, termination, and paragraph indexing.
/// </summary>
[TestFixture]
public class BlockQuoteParserTests
{
    // -------------------------------------------------------------------------
    // Basic recognition
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleLineBlockQuote_ProducesBlockQuoteParagraph()
    {
        const string markdown = "> Hello\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
    }

    [Test]
    public void Parse_SingleLineBlockQuote_ContentStripsMarkerAndSpace()
    {
        const string markdown = "> Hello\n";

        var model = ParseOk(markdown);
        var bq = (DM.BlockQuote)model.TopLevelSection.Paragraphs[0];

        bq.Content.Value.Should().Be("Hello");
    }

    [Test]
    public void Parse_BlockQuoteWithNoSpaceAfterMarker_ContentStripsMarker()
    {
        // CommonMark allows >text with no space
        const string markdown = ">Hello\n";

        var model = ParseOk(markdown);
        var bq = (DM.BlockQuote)model.TopLevelSection.Paragraphs[0];

        bq.Content.Value.Should().Be("Hello");
    }

    // -------------------------------------------------------------------------
    // Multi-line block quote: all lines are part of one BlockQuote paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_MultiLineBlockQuote_ProducesOneBlockQuoteParagraph()
    {
        const string markdown = "> Line one\n> Line two\n> Line three\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
    }

    [Test]
    public void Parse_MultiLineBlockQuote_ContentContainsAllLines()
    {
        const string markdown = "> Line one\n> Line two\n> Line three\n";

        var model = ParseOk(markdown);
        var bq = (DM.BlockQuote)model.TopLevelSection.Paragraphs[0];

        bq.Content.Value.Should().Contain("Line one");
        bq.Content.Value.Should().Contain("Line two");
        bq.Content.Value.Should().Contain("Line three");
    }

    // -------------------------------------------------------------------------
    // Termination: block quote ends on the first non-'>' line
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BlockQuoteFollowedByText_ProducesTwoParagraphs()
    {
        const string markdown = "> Quoted.\nNot quoted.\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.TextBlock>();
    }

    [Test]
    public void Parse_BlockQuoteFollowedByText_TextParagraphContentIsCorrect()
    {
        const string markdown = "> Quoted.\nNot quoted.\n";

        var model = ParseOk(markdown);
        var text = (DM.TextBlock)model.TopLevelSection.Paragraphs[1];

        text.Content.Value.Should().Be("Not quoted.");
    }

    [Test]
    public void Parse_BlockQuoteFollowedByHeading_BlockQuoteEndsBeforeHeading()
    {
        const string markdown = "> Quoted.\n# Next Section\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
        model.TopLevelSection.Children.Should().HaveCount(1);
        model.TopLevelSection.Children[0].Heading.Text.Value.Should().Be("Next Section");
    }

    // -------------------------------------------------------------------------
    // Termination by blank line: a blank line between > lines ends the block quote.
    // This documents current behaviour — the parser does not implement
    // CommonMark lazy continuation across blank lines.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BlockQuoteWithBlankLineBetweenQuotedLines_ProducesTwoSeparateBlockQuotes()
    {
        const string markdown = "> First.\n\n> Second.\n";

        var model = ParseOk(markdown);

        // The blank line terminates the first block quote; the second > starts a new one.
        model.TopLevelSection.Paragraphs.Should().HaveCount(2,
            "a blank line between block quote lines should terminate the first block quote");
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.BlockQuote>();
    }

    // -------------------------------------------------------------------------
    // Paragraph index
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TextThenBlockQuote_BlockQuoteParagraphIndexIsTwo()
    {
        const string markdown = "Intro.\n\n> Quoted.\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs[0].Index.Should().Be(1);
        model.TopLevelSection.Paragraphs[1].Index.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Multi-line block quote: remainder buffer is positioned correctly after the block
    // so subsequent paragraphs are parsed from the right place.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_MultiLineBlockQuoteThenText_TextParagraphIsParsedCorrectly()
    {
        const string markdown = "> Line one\n> Line two\nAfter quote.\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();

        var text = model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.TextBlock>().Subject;
        text.Content.Value.Should().Be("After quote.",
            "the remainder buffer after a multi-line block quote must point to the correct position");
    }

    [Test]
    public void Parse_ThreeLineBlockQuoteThenText_TextParagraphIsParsedCorrectly()
    {
        const string markdown = "> One\n> Two\n> Three\nAfter.\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        var text = model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.TextBlock>().Subject;
        text.Content.Value.Should().Be("After.",
            "the remainder buffer after a three-line block quote must be positioned correctly");
    }

    // -------------------------------------------------------------------------
    // Nested block quotes: >> is treated as a deeper quote level.
    // Content of >> and > lines should be distinguishable.
    // This documents the current behaviour where nesting depth is collapsed.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_DoubleMarkerBlockQuote_ProducesSingleBlockQuoteParagraph()
    {
        // >> is still a block quote; the parser should not crash or produce a TextBlock
        const string markdown = ">> Nested quote.\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
    }

    [Test]
    public void Parse_MixedNestingBlockQuote_LinesAreConcatenatedWithNoSeparator()
    {
        // A line with > followed by >> — both start with > so both are consumed
        // into the same block quote. The > and >> markers are stripped, leaving
        // the text content of each line concatenated with no newline between them.
        // This is a known limitation: the parser currently does not preserve
        // per-line structure or nesting depth inside a block quote.
        const string markdown = "> Outer.\n>> Inner.\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        var bq = (DM.BlockQuote)model.TopLevelSection.Paragraphs[0];
        bq.Content.Value.Should().Be("Outer.Inner.",
            "multi-line block quote content is currently concatenated with no separator between lines");
    }

    // -------------------------------------------------------------------------
    // Block quote among other paragraph types
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ListThenBlockQuote_ProducesTwoDistinctParagraphs()
    {
        const string markdown = "- Item\n> Quoted.\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.BlockQuote>();
    }

    [Test]
    public void Parse_BlockQuoteThenList_ProducesTwoDistinctParagraphs()
    {
        const string markdown = "> Quoted.\n- Item\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.ListBlock>();
    }

    // -------------------------------------------------------------------------
    // CRLF line endings: block quote parsing must handle \r\n the same as \n
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BlockQuote_CrLfLineEndings_ProducesSingleBlockQuoteParagraph()
    {
        const string markdown = "> Line one\r\n> Line two\r\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
    }

    [Test]
    public void Parse_BlockQuote_CrLfLineEndings_ContentContainsBothLines()
    {
        const string markdown = "> Line one\r\n> Line two\r\n";

        var model = ParseOk(markdown);
        var bq = (DM.BlockQuote)model.TopLevelSection.Paragraphs[0];

        bq.Content.Value.Should().Contain("Line one");
        bq.Content.Value.Should().Contain("Line two");
    }

    // -------------------------------------------------------------------------
    // Block quote followed immediately by a fenced code block (no blank line)
    // The ``` line does not start with >, so the block quote must terminate.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BlockQuoteFollowedImmediatelyByFencedCodeBlock_BlockQuoteTerminates()
    {
        const string markdown = "> Quoted.\n```\ncode\n```\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2,
            "the ``` line does not start with > so it should terminate the block quote");
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.CodeBlock>();
    }

    // -------------------------------------------------------------------------
    // Round-trip limitation: BlockQuote.Content is a collapsed string with no
    // per-line marker or line-ending data. Re-adding the > prefix per line
    // is the best reconstruction available.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleLineBlockQuote_ContentDoesNotContainMarkerOrLineEnding()
    {
        // The parser strips the leading "> " from each line and concatenates.
        // This means the Content value does not include the marker or the newline,
        // so a direct round-trip to the original source is not possible from Content alone.
        const string markdown = "> Hello\n";

        var model = ParseOk(markdown);
        var bq = (DM.BlockQuote)model.TopLevelSection.Paragraphs[0];

        bq.Content.Value.Should().NotContain(">",
            "the > marker should be stripped from the content");
        bq.Content.Value.Should().NotContain("\n",
            "the line ending should not appear in the stripped content of a single-line block quote");
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static DM.MarkdownDocument ParseOk(string markdown)
    {
        var result = DM.MarkdownParser.Parse(markdown);
        result.Should().BeOfType<Result<DM.MarkdownDocument, MdqError>.Ok>();
        return ((Result<DM.MarkdownDocument, MdqError>.Ok)result).Value;
    }
}
