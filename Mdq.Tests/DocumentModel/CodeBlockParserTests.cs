using AwesomeAssertions;
using Mdq.Core.Shared;
using DM = Mdq.Core.DocumentModel;

namespace Mdq.Tests.DocumentModel;

/// <summary>
/// Tests for fenced and indented code block parsing, language tags, content, and trivia.
/// </summary>
[TestFixture]
public class CodeBlockParserTests
{
    // -------------------------------------------------------------------------
    // Fenced code block: basic recognition
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlock_ProducesCodeBlockParagraph()
    {
        const string markdown = """
            ```
            some code
            ```
            """;

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>();
    }

    [Test]
    public void Parse_FencedCodeBlock_ContentIsCorrect()
    {
        const string markdown = "```\nsome code\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Lines[0].Value.Should().Contain("some code");
    }

    [Test]
    public void Parse_FencedCodeBlock_MultiLineContent_AllLinesPresent()
    {
        const string markdown = "```\nline one\nline two\nline three\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Lines[0].Value.Should().Contain("line one");
        block.Lines[1].Value.Should().Contain("line two");
        block.Lines[2].Value.Should().Contain("line three");
    }

    // -------------------------------------------------------------------------
    // Fenced code block: language tag
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlockWithLanguage_LanguageIsCorrect()
    {
        const string markdown = "```csharp\nvar x = 1;\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.FencedCodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Language.Value.Should().Be("csharp");
    }

    [Test]
    public void Parse_FencedCodeBlockNoLanguage_LanguageIsEmpty()
    {
        const string markdown = "```\ncode\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.FencedCodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Language.Value.Should().BeNullOrEmpty();
    }

    [Test]
    public void Parse_FencedCodeBlockWithLanguage_ContentDoesNotIncludeLanguageTag()
    {
        const string markdown = "```python\nprint('hello')\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Lines[0].Value.Should().NotContain("python");
    }

    // -------------------------------------------------------------------------
    // Fenced code block: leading trivia is the opening fence marker
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlock_LeadingTriviaIsOpeningFence()
    {
        const string markdown = "```csharp\ncode\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.LeadingTrivia.Value.Should().Be("```",
            "the opening fence marker should be stored in LeadingTrivia");
    }

    // -------------------------------------------------------------------------
    // Fenced code block: paragraph index
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TextBlockThenFencedCodeBlock_CodeBlockIndexIsTwo()
    {
        const string markdown = "Intro.\n\n```\ncode\n```\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs[0].Index.Should().Be(1);
        model.TopLevelSection.Paragraphs[1].Index.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Fenced code block: terminated by heading
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlockFollowedByHeading_CodeBlockEndsBeforeHeading()
    {
        const string markdown = "```\ncode\n```\n# Next\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>();
        model.TopLevelSection.Children.Should().HaveCount(1);
    }

    // -------------------------------------------------------------------------
    // Fenced code block: unterminated (no closing fence)
    // The block runs to the end of the input without crashing.
    // This documents current behaviour; exact content is implementation-defined.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_UnterminatedFencedCodeBlock_DoesNotThrow()
    {
        const string markdown = "```\ncode without closing fence\n";

        var act = () => ParseOk(markdown);

        act.Should().NotThrow("an unterminated fenced code block should not crash the parser");
    }

    [Test]
    public void Parse_UnterminatedFencedCodeBlock_ProducesCodeBlockNotTextBlock()
    {
        const string markdown = "```\ncode without closing fence\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>(
            "a fenced code block that is never closed should still be parsed as a CodeBlock");
    }

    // -------------------------------------------------------------------------
    // Fenced code block: closing fence at very end of buffer with no trailing newline
    // Exercises the buffer.Length - 3 boundary in the closing-fence search loop.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlockClosingFenceAtEndOfBuffer_ContentIsCorrect()
    {
        // No trailing newline after the closing ```
        const string markdown = "```\ncode\n```";

        var model = ParseOk(markdown);
        var block = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>().Subject;

        block.Lines[0].Value.Should().Contain("code");
    }

    // -------------------------------------------------------------------------
    // Fenced code block: content containing backticks that are not the closing fence
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlockWithInternalBackticks_ContentPreserved()
    {
        const string markdown = "```\nvar s = `template`;\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Lines[0].Value.Should().Contain("`template`");
    }

    [Test]
    public void Parse_FencedCodeBlockWithTwoBackticksInContent_NotMistakenForClosingFence()
    {
        const string markdown = "```\na``b\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Lines[0].Value.Should().Contain("a``b");
    }

    // -------------------------------------------------------------------------
    // Indented code block: basic recognition
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_IndentedCodeBlock_ProducesCodeBlockParagraph()
    {
        const string markdown = "    indented code\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.IndentedCodeBlock>();
    }

    [Test]
    public void Parse_IndentedCodeBlock_ContentStripsFristLineIndent()
    {
        const string markdown = "    indented code\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Lines[0].Value.Should().Contain("indented code");
        block.Lines[0].Value.Should().NotStartWith(" ",
            "the leading indent should be stripped from the content");
    }

    [Test]
    public void Parse_IndentedCodeBlock_MultiLineContent_AllLinesPresent()
    {
        const string markdown = "    line one\n    line two\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Lines[0].Value.Should().Contain("line one");
        block.Lines[1].Value.Should().Contain("line two");
    }

    [Test]
    public void Parse_IndentedCodeBlock_LeadingTriviaIsIndent()
    {
        const string markdown = "    code\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.LeadingTrivia.Value.Should().Be("    ",
            "leading trivia for an indented code block should be the indent of the first line");
    }

    // -------------------------------------------------------------------------
    // Indented code block: terminates on blank line
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_IndentedCodeBlockFollowedByBlankLine_EndsAtBlankLine()
    {
        const string markdown = "    line one\n\n    line two\n";

        var model = ParseOk(markdown);

        // The blank line terminates the first block; the second indented block is separate
        model.TopLevelSection.Paragraphs.Should().HaveCount(2,
            "a blank line between indented blocks should produce two separate CodeBlock paragraphs");
    }

    // -------------------------------------------------------------------------
    // Indented code block: second line shorter than first line's indent
    // This exercises the ArgumentOutOfRangeException bug in ParseIndentedCodeBlock.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_IndentedCodeBlock_SubsequentLineHasLessIndent_DoesNotThrow()
    {
        // First line has 4 spaces; second line has only 2.
        // ParseIndentedCodeBlock does buffer.Subsegment(0, firstLineIndent.Length) on each line,
        // which throws when the line is shorter than firstLineIndent.Length.
        const string markdown = "    four spaces\n  two spaces\n";

        var act = () => ParseOk(markdown);

        act.Should().NotThrow("a subsequent line with less indent than the first should terminate the block, not throw");
    }

    [Test]
    public void Parse_IndentedCodeBlock_SubsequentLineHasLessIndent_FirstLineBecomesCodeBlock()
    {
        const string markdown = "    four spaces\n  two spaces\n";

        var model = ParseOk(markdown);

        // Even if only the first line is captured, it should be a CodeBlock
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.IndentedCodeBlock>();
    }

    // -------------------------------------------------------------------------
    // Indented code block: second line is shorter than the indent length in total
    // This is the case that actually triggers the ArgumentOutOfRangeException:
    // line.Length < firstLineIndent.Length means Subsegment(0, firstLineIndent.Length) throws.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_IndentedCodeBlock_SubsequentLineShorterThanIndentLength_DoesNotThrow()
    {
        // First line has 4 spaces of indent. Second line is only 3 characters total ("ab\n"),
        // so line.Length (2) < firstLineIndent.Length (4). This triggers an
        // ArgumentOutOfRangeException in line.Subsegment(0, 4).
        const string markdown = "    code\nab\n";

        var act = () => ParseOk(markdown);

        act.Should().NotThrow("a subsequent line shorter than the first-line indent must not cause an ArgumentOutOfRangeException");
    }

    [Test]
    public void Parse_IndentedCodeBlock_SubsequentLineShorterThanIndentLength_FirstLineBecomesCodeBlock()
    {
        const string markdown = "    code\nab\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.IndentedCodeBlock>(
            "the first indented line should still produce a CodeBlock even when the second line is too short");
    }

    // -------------------------------------------------------------------------
    // Indented code block: single leading space is treated as an indented code block
    // This documents the current behaviour — no minimum indent of 4 is enforced.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleLeadingSpace_IsNotACodeBlockBecauseTheSpecRequires4Spaces()
    {
        const string markdown = " one space\n";

        var model = ParseOk(markdown);

        // Document current behaviour: any leading whitespace triggers indented code block detection.
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>(
            "the parser currently treats any leading whitespace as an indented code block, with no 4-space minimum");
    }

    // -------------------------------------------------------------------------
    // Two fenced code blocks separated by a blank line are distinct paragraphs
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TwoFencedCodeBlocks_ProduceTwoDistinctCodeBlocks()
    {
        const string markdown = "```\nfirst\n```\n\n```\nsecond\n```\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.FencedCodeBlock>();
    }

    // -------------------------------------------------------------------------
    // Fenced code block: four backticks in content
    // The closing-fence scanner matches the first ``` it finds. Four consecutive
    // backticks contain a valid ``` at index 0 (matching positions 0,1,2),
    // which would be found before the actual closing fence on its own line.
    // This documents whether the parser handles this case correctly.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlockWithFourBackticksOnLine_ClosingFenceIsNotTriggeredMidLine()
    {
        // The content line ```` contains a run of 4 backticks.
        // The scanner should not treat the first ``` in that run as the closing fence,
        // because a closing fence must be on its own line.
        // NOTE: The current parser scans byte-by-byte without a line boundary check,
        // so this test documents whether that causes incorrect early termination.
        const string markdown = "```\n````\nmore\n```\n";

        var model = ParseOk(markdown);
        var block = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>().Subject;

        block.Lines[1].Value.Should().Contain("more",
            "the four-backtick line should not be mistaken for the closing fence, so 'more' should appear in content");
    }

    // -------------------------------------------------------------------------
    // Fenced code block: CRLF line endings work identically to LF
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlock_CrLfLineEndings_ProducesCorrectContent()
    {
        const string markdown = "```\r\nline one\r\nline two\r\n```\r\n";

        var model = ParseOk(markdown);
        var block = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>().Subject;

        block.Lines[0].Value.Should().Contain("line one");
        block.Lines[1].Value.Should().Contain("line two");
    }

    [Test]
    public void Parse_FencedCodeBlock_CrLfLineEndings_LanguageTagIsCorrect()
    {
        const string markdown = "```csharp\r\nvar x = 1;\r\n```\r\n";

        var model = ParseOk(markdown);
        var block = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.FencedCodeBlock>().Subject;

        block.Language.Value.Should().Be("csharp");
    }

    // -------------------------------------------------------------------------
    // Fenced code block immediately after a block quote (no blank line)
    // The block quote should terminate when it sees the ``` line.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BlockQuoteImmediatelyFollowedByFencedCodeBlock_TwoParagraphsProduced()
    {
        const string markdown = "> Quoted.\n```\ncode\n```\n";

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2,
            "the ``` fence should terminate the block quote and start a new CodeBlock paragraph");
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.BlockQuote>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.FencedCodeBlock>();
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
