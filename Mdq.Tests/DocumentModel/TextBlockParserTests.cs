using AwesomeAssertions;
using Mdq.Core.Shared;
using DM = Mdq.Core.DocumentModel;

namespace Mdq.Tests.DocumentModel;

/// <summary>
/// Tests for text block (paragraph) recognition, multi-line continuations,
/// and the trivia attached to text blocks.
/// </summary>
[TestFixture]
public class TextBlockParserTests
{
    // -------------------------------------------------------------------------
    // Single paragraph, single line
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleLine_ProducesOneTextBlock()
    {
        const string markdown = "Hello, world.";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>()
            .Which.Content.Value.Should().Be("Hello, world.");
    }

    // -------------------------------------------------------------------------
    // Blank-line separation creates distinct paragraphs
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TwoLinesSeparatedByBlankLine_ProducesTwoTextBlocks()
    {
        const string markdown = "First.\n\nSecond.";

        var model = ParseOk(markdown);

        var paras = model.TopLevelSection.Paragraphs;
        paras.Should().HaveCount(2);
        paras[0].Should().BeOfType<DM.TextBlock>().Which.Content.Value.Should().Be("First.");
        paras[1].Should().BeOfType<DM.TextBlock>().Which.Content.Value.Should().Be("Second.");
    }

    [Test]
    public void Parse_TwoLinesSeparatedByMultipleBlankLines_ProducesTwoTextBlocks()
    {
        const string markdown = "First.\n\n\n\nSecond.";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // Consecutive non-blank lines form a single paragraph (soft wrapping)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TwoConsecutiveLines_ProducesSingleTextBlock()
    {
        const string markdown = "Line one.\nLine two.";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        var tb = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>().Subject;
        tb.Content.Value.Should().Contain("Line one.");
        tb.Content.Value.Should().Contain("Line two.");
    }

    // -------------------------------------------------------------------------
    // A heading in the middle terminates the current paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ParagraphFollowedImmediatelyByHeading_HeadingStartsNewSection()
    {
        const string markdown = "Preamble text.\n# Heading\nBody.";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1,
            "the preamble paragraph should be on the top-level section");
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>()
            .Which.Content.Value.Should().Be("Preamble text.");

        model.TopLevelSection.Children.Should().HaveCount(1);
        model.TopLevelSection.Children[0].Heading.Text.Value.Should().Be("Heading");
    }

    // -------------------------------------------------------------------------
    // Paragraph index is 1-based and increments within each section
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ThreeParagraphs_IndicesAreOneBasedAndSequential()
    {
        const string markdown = "One.\n\nTwo.\n\nThree.";

        var model = ParseOk(markdown);

        var paras = model.TopLevelSection.Paragraphs;
        paras.Should().HaveCount(3);
        paras[0].Index.Should().Be(1);
        paras[1].Index.Should().Be(2);
        paras[2].Index.Should().Be(3);
    }

    [Test]
    public void Parse_ParagraphIndexRestartsInEachSection()
    {
        const string markdown = """
            # A
            Alpha one.

            Alpha two.

            # B
            Beta one.
            """;

        var model = ParseOk(markdown);

        var sectionA = model.TopLevelSection.Children[0];
        var sectionB = model.TopLevelSection.Children[1];

        sectionA.Paragraphs[0].Index.Should().Be(1);
        sectionA.Paragraphs[1].Index.Should().Be(2);
        sectionB.Paragraphs[0].Index.Should().Be(1,
            "paragraph index should restart at 1 for each new section");
    }

    // -------------------------------------------------------------------------
    // TextBlock Content StringSegment is a slice of the original string (no allocation)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleLineParagraph_ContentSegmentReferencesOriginalString()
    {
        const string markdown = "Hello.";

        var model = ParseOk(markdown);

        var tb = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>().Subject;
        // StringSegment.Buffer should be the same reference as the input string
        tb.Content.Buffer.Should().BeSameAs(markdown,
            "StringSegment should reference the original string without copying");
    }

    // -------------------------------------------------------------------------
    // TrailingTrivia captures the line ending after the paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ParagraphFollowedByLF_TrailingTriviaIsLineFeed()
    {
        const string markdown = "Text.\nNext.";

        var model = ParseOk(markdown);

        // The first paragraph ("Text.") is immediately followed by a non-blank line,
        // so it is joined with "Next." into one paragraph. Use blank-line version:
        var twoParas = "Text.\n\nNext.";
        var twoModel = ParseOk(twoParas);

        // The first paragraph's trailing trivia should include the blank line ("\n\n").
        var firstBlock = twoModel.TopLevelSection.Paragraphs[0];
        firstBlock.TrailingTrivia.HasValue.Should().BeTrue();
        firstBlock.TrailingTrivia.Value.Should().Contain("\n");
    }

    // -------------------------------------------------------------------------
    // Inline markup is preserved verbatim (parser does not interpret inline syntax)
    // -------------------------------------------------------------------------

    [TestCase("**bold**")]
    [TestCase("*italic*")]
    [TestCase("`code`")]
    [TestCase("_underline_")]
    [TestCase("~~strikethrough~~")]
    [TestCase("[link](http://example.com)")]
    [TestCase("![image](photo.png)")]
    public void Parse_InlineMarkup_IsPreservedVerbatim(string inlineMarkdown)
    {
        var model = ParseOk(inlineMarkdown);

        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>()
            .Which.Content.Value.Should().Be(inlineMarkdown,
                "inline markup should be stored as-is; the parser is structural, not inline-aware");
    }

    // -------------------------------------------------------------------------
    // A line beginning with > does NOT continue a preceding text paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BlockQuoteImmediatelyAfterTextBlock_ProducesTwoDistinctParagraphs()
    {
        const string markdown = "Intro text.\n> Quoted.";

        var model = ParseOk(markdown);

        var paras = model.TopLevelSection.Paragraphs;
        paras.Should().HaveCount(2,
            "a blockquote marker should terminate the preceding text paragraph");
        paras[0].Should().BeOfType<DM.TextBlock>();
        paras[1].Should().BeOfType<DM.BlockQuote>();
    }

    // -------------------------------------------------------------------------
    // A line beginning with a list marker does NOT continue a preceding text paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BulletedListImmediatelyAfterTextBlock_ProducesTwoDistinctParagraphs()
    {
        const string markdown = "Intro text.\n- Item one.";

        var model = ParseOk(markdown);

        var paras = model.TopLevelSection.Paragraphs;
        paras.Should().HaveCount(2,
            "a list marker should terminate the preceding text paragraph");
        paras[0].Should().BeOfType<DM.TextBlock>();
        paras[1].Should().BeOfType<DM.ListBlock>();
    }

    // -------------------------------------------------------------------------
    // A line beginning with a fenced code block marker does NOT continue a text paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlockImmediatelyAfterTextBlock_ProducesTwoDistinctParagraphs()
    {
        const string markdown = "Intro text.\n```\ncode here\n```";

        var model = ParseOk(markdown);

        var paras = model.TopLevelSection.Paragraphs;
        paras.Should().HaveCount(2,
            "a fenced code block should terminate the preceding text paragraph");
        paras[0].Should().BeOfType<DM.TextBlock>();
        paras[1].Should().BeOfType<DM.FencedCodeBlock>();
    }

    // -------------------------------------------------------------------------
    // CRLF line endings work identically to LF
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_CrLfLineEndings_ProduceSameStructureAsLf()
    {
        const string lfMarkdown = "One.\n\nTwo.";
        const string crlfMarkdown = "One.\r\n\r\nTwo.";

        var lfModel = ParseOk(lfMarkdown);
        var crlfModel = ParseOk(crlfMarkdown);

        crlfModel.TopLevelSection.Paragraphs.Should().HaveCount(
            lfModel.TopLevelSection.Paragraphs.Count,
            "CRLF and LF endings should produce the same document structure");

        crlfModel.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>()
            .Which.Content.Value.Should().Be("One.");
        crlfModel.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.TextBlock>()
            .Which.Content.Value.Should().Be("Two.");
    }

    // -------------------------------------------------------------------------
    // Round-trip: reassembling LeadingTrivia + Content + TrailingTrivia reproduces the source block
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleParagraphDocument_RoundTripsToOriginalSource()
    {
        const string markdown = "Hello, world.\n";

        var model = ParseOk(markdown);

        var tb = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>().Subject;
        var reassembled = (tb.LeadingTrivia.Value ?? "")
                        + (tb.Content.Value ?? "")
                        + (tb.TrailingTrivia.Value ?? "");

        reassembled.Should().Be(markdown,
            "LeadingTrivia + Content + TrailingTrivia must reproduce the original source");
    }

    [Test]
    public void Parse_TwoParagraphDocument_RoundTripsToOriginalSource()
    {
        const string markdown = "First.\n\nSecond.\n";

        var model = ParseOk(markdown);

        var paras = model.TopLevelSection.Paragraphs;
        var reassembled = string.Concat(paras.Select(p =>
        {
            var tb = (DM.TextBlock)p;
            return (tb.LeadingTrivia.Value ?? "")
                 + (tb.Content.Value ?? "")
                 + (tb.TrailingTrivia.Value ?? "");
        }));

        reassembled.Should().Be(markdown,
            "concatenating trivia+content+trivia for every paragraph should reproduce the original source");
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
