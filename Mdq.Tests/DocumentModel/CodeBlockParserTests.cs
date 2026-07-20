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
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.CodeBlock>();
    }

    [Test]
    public void Parse_FencedCodeBlock_ContentIsCorrect()
    {
        const string markdown = "```\nsome code\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Content.Value.Should().Contain("some code");
    }

    [Test]
    public void Parse_FencedCodeBlock_MultiLineContent_AllLinesPresent()
    {
        const string markdown = "```\nline one\nline two\nline three\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Content.Value.Should().Contain("line one");
        block.Content.Value.Should().Contain("line two");
        block.Content.Value.Should().Contain("line three");
    }

    // -------------------------------------------------------------------------
    // Fenced code block: language tag
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_FencedCodeBlockWithLanguage_LanguageIsCorrect()
    {
        const string markdown = "```csharp\nvar x = 1;\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Language.Value.Should().Be("csharp");
    }

    [Test]
    public void Parse_FencedCodeBlockNoLanguage_LanguageIsEmpty()
    {
        const string markdown = "```\ncode\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Language.Value.Should().BeNullOrEmpty();
    }

    [Test]
    public void Parse_FencedCodeBlockWithLanguage_ContentDoesNotIncludeLanguageTag()
    {
        const string markdown = "```python\nprint('hello')\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Content.Value.Should().NotContain("python");
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
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.CodeBlock>();
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
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.CodeBlock>(
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
        var block = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.CodeBlock>().Subject;

        block.Content.Value.Should().Contain("code");
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

        block.Content.Value.Should().Contain("`template`");
    }

    [Test]
    public void Parse_FencedCodeBlockWithTwoBackticksInContent_NotMistakenForClosingFence()
    {
        const string markdown = "```\na``b\n```\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Content.Value.Should().Contain("a``b");
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
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.CodeBlock>();
    }

    [Test]
    public void Parse_IndentedCodeBlock_ContentStripsFristLineIndent()
    {
        const string markdown = "    indented code\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Content.Value.Should().Contain("indented code");
        block.Content.Value.Should().NotStartWith(" ",
            "the leading indent should be stripped from the content");
    }

    [Test]
    public void Parse_IndentedCodeBlock_MultiLineContent_AllLinesPresent()
    {
        const string markdown = "    line one\n    line two\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Content.Value.Should().Contain("line one");
        block.Content.Value.Should().Contain("line two");
    }

    [Test]
    public void Parse_IndentedCodeBlock_LanguageIsEmpty()
    {
        const string markdown = "    code\n";

        var model = ParseOk(markdown);
        var block = (DM.CodeBlock)model.TopLevelSection.Paragraphs[0];

        block.Language.Value.Should().BeNullOrEmpty();
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
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.CodeBlock>();
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
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.CodeBlock>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.CodeBlock>();
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
