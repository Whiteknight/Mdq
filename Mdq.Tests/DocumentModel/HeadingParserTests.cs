using AwesomeAssertions;
using Mdq.Core.Shared;
using DM = Mdq.Core.DocumentModel;

namespace Mdq.Tests.DocumentModel;

/// <summary>
/// Tests for ATX heading recognition and the trivia (leading/trailing whitespace) attached to headings.
/// </summary>
[TestFixture]
public class HeadingParserTests
{
    // -------------------------------------------------------------------------
    // Basic heading level recognition
    // -------------------------------------------------------------------------

    [TestCase("# H1", 1, "H1")]
    [TestCase("## H2", 2, "H2")]
    [TestCase("### H3", 3, "H3")]
    [TestCase("#### H4", 4, "H4")]
    [TestCase("##### H5", 5, "H5")]
    [TestCase("###### H6", 6, "H6")]
    public void Parse_HeadingLevelOneToSix_CorrectLevelAndText(string line, int expectedLevel, string expectedText)
    {
        var model = ParseOk(line);

        var section = model.TopLevelSection.Children[0];
        section.Heading.Level.Should().Be(expectedLevel);
        section.Heading.Text.Value.Should().Be(expectedText);
    }

    // -------------------------------------------------------------------------
    // Seven or more # characters is NOT a heading (CommonMark spec)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SevenHashes_IsNotAHeading_TreatedAsParagraph()
    {
        const string markdown = "####### Not a heading";

        var model = ParseOk(markdown);

        // Should produce zero heading-backed sections; the line ends up as a text paragraph on the top-level section.
        model.TopLevelSection.Children.Should().BeEmpty("seven # chars is not a valid ATX heading");
        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>();
    }

    // -------------------------------------------------------------------------
    // A # with no space after it is NOT a heading (CommonMark spec)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HashWithNoSpaceAfter_IsNotAHeading_TreatedAsParagraph()
    {
        const string markdown = "#NoSpace";

        var model = ParseOk(markdown);

        model.TopLevelSection.Children.Should().BeEmpty("#NoSpace is not a valid ATX heading");
        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.TextBlock>()
            .Which.Content.Value.Should().Be("#NoSpace");
    }

    [Test]
    public void Parse_DoubleHashWithNoSpaceAfter_IsNotAHeading()
    {
        const string markdown = "##AlsoNotAHeading";

        var model = ParseOk(markdown);

        model.TopLevelSection.Children.Should().BeEmpty();
        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
    }

    // -------------------------------------------------------------------------
    // Heading with no body text (heading-only line)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HeadingWithNoText_ProducesSectionWithEmptyHeadingText()
    {
        const string markdown = "# ";

        var model = ParseOk(markdown);

        var section = model.TopLevelSection.Children[0];
        section.Heading.Level.Should().Be(1);
        section.Heading.Text.Value.Should().BeNullOrEmpty();
    }

    [Test]
    public void Parse_HeadingWithJustHashes_ProducesSectionWithEmptyHeadingText()
    {
        const string markdown = "#";

        var model = ParseOk(markdown);

        // A lone # with nothing after is a valid level-1 heading with empty text.
        var section = model.TopLevelSection.Children[0];
        section.Heading.Level.Should().Be(1);
        section.Heading.Text.Value.Should().BeNullOrEmpty();
    }

    // -------------------------------------------------------------------------
    // Optional closing # sequence (CommonMark allows "## Heading ##")
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HeadingWithClosingHashes_HeadingTextDoesNotContainTrailingHashes()
    {
        const string markdown = "## Heading ##";

        var model = ParseOk(markdown);

        var section = model.TopLevelSection.Children[0];
        section.Heading.Text.Value.Should().Be("Heading",
            "trailing # sequence should be stripped per CommonMark spec");
    }

    [Test]
    public void Parse_HeadingWithClosingHashesDifferentCount_HeadingTextDoesNotContainTrailingHashes()
    {
        const string markdown = "# Heading ####";

        var model = ParseOk(markdown);

        var section = model.TopLevelSection.Children[0];
        section.Heading.Text.Value.Should().Be("Heading",
            "closing sequence does not have to match the opening count");
    }

    // -------------------------------------------------------------------------
    // Heading text is preserved exactly (no accidental trimming of interior whitespace)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HeadingTextWithInternalSpaces_SpacesArePreserved()
    {
        const string markdown = "# Foo Bar Baz";

        var model = ParseOk(markdown);

        model.TopLevelSection.Children[0].Heading.Text.Value.Should().Be("Foo Bar Baz");
    }

    // -------------------------------------------------------------------------
    // Heading LeadingTrivia contains the "## " prefix characters
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_Heading_LeadingTriviaContainsHashesAndSpace()
    {
        const string markdown = "## My Heading";

        var model = ParseOk(markdown);

        var heading = model.TopLevelSection.Children[0].Heading;
        heading.LeadingTrivia.Value.Should().Be("## ",
            "leading trivia should capture the hashes and the separating space for round-trip fidelity");
    }

    // -------------------------------------------------------------------------
    // Heading TrailingTrivia contains the line ending
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HeadingFollowedByLF_TrailingTriviaIsLineFeed()
    {
        const string markdown = "# Alpha\nBody.";

        var model = ParseOk(markdown);

        var heading = model.TopLevelSection.Children[0].Heading;
        heading.TrailingTrivia.Value.Should().Be("\n",
            "trailing trivia should capture the newline after the heading text");
    }

    [Test]
    public void Parse_HeadingFollowedByCrLf_TrailingTriviaIsCrLf()
    {
        const string markdown = "# Alpha\r\nBody.";

        var model = ParseOk(markdown);

        var heading = model.TopLevelSection.Children[0].Heading;
        heading.TrailingTrivia.Value.Should().Be("\r\n",
            "trailing trivia should capture CRLF endings intact for round-trip fidelity");
    }

    // -------------------------------------------------------------------------
    // Round-trip: reassembling LeadingTrivia + Text + TrailingTrivia reproduces the source line
    // -------------------------------------------------------------------------

    [TestCase("# Simple\n")]
    [TestCase("## Two Words\n")]
    [TestCase("### Three Level\n")]
    [TestCase("# Simple\r\n")]
    public void Parse_Heading_TriviaAndTextRoundTripsToSourceLine(string markdownLine)
    {
        var model = ParseOk(markdownLine);

        var heading = model.TopLevelSection.Children[0].Heading;
        var reassembled = (heading.LeadingTrivia.Value ?? "")
                        + (heading.Text.Value ?? "")
                        + (heading.TrailingTrivia.Value ?? "");

        reassembled.Should().Be(markdownLine,
            "LeadingTrivia + Text + TrailingTrivia must reproduce the original source");
    }

    // -------------------------------------------------------------------------
    // Paragraph index assignment: each section's paragraphs are 1-indexed
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SectionWithTwoParagraphs_ParagraphIndicesAreOneAndTwo()
    {
        const string markdown = """
            # Heading
            First.

            Second.
            """;

        var model = ParseOk(markdown);

        var section = model.TopLevelSection.Children[0];
        section.Paragraphs[0].Index.Should().Be(1);
        section.Paragraphs[1].Index.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Siblings: headings at the same level are siblings, not nested
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TwoH2UnderSameH1_BothAreChildrenOfH1()
    {
        const string markdown = """
            # Root
            ## Alpha
            ## Beta
            """;

        var model = ParseOk(markdown);

        var root = model.TopLevelSection.Children[0];
        root.Children.Should().HaveCount(2);
        root.Children[0].Heading.Text.Value.Should().Be("Alpha");
        root.Children[1].Heading.Text.Value.Should().Be("Beta");
    }

    [Test]
    public void Parse_H3AfterH2AfterH1_CorrectlyNested()
    {
        const string markdown = """
            # One
            ## Two
            ### Three
            """;

        var model = ParseOk(markdown);

        var h1 = model.TopLevelSection.Children[0];
        h1.Heading.Level.Should().Be(1);

        var h2 = h1.Children[0];
        h2.Heading.Level.Should().Be(2);

        var h3 = h2.Children[0];
        h3.Heading.Level.Should().Be(3);
        h3.Children.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // A heading that jumps levels (H1 -> H3) still nests correctly
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_H3DirectlyUnderH1_SkippingH2_IsChildOfH1()
    {
        const string markdown = """
            # Top
            ### Deep
            Body.
            """;

        var model = ParseOk(markdown);

        var h1 = model.TopLevelSection.Children[0];
        // H3 should still be nested under H1 even though H2 was skipped.
        h1.Children.Should().HaveCount(1);
        h1.Children[0].Heading.Level.Should().Be(3);
        h1.Children[0].Heading.Text.Value.Should().Be("Deep");
    }

    // -------------------------------------------------------------------------
    // A lower-level heading after a higher one closes the higher section
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_H2AfterH3_H2IsSiblingOfH2NotChildOfH3()
    {
        const string markdown = """
            # Root
            ## Parent
            ### Child
            ## Sibling
            """;

        var model = ParseOk(markdown);

        var root = model.TopLevelSection.Children[0];
        root.Children.Should().HaveCount(2, "both ## headings are direct children of # Root");
        root.Children[0].Heading.Text.Value.Should().Be("Parent");
        root.Children[1].Heading.Text.Value.Should().Be("Sibling");

        root.Children[0].Children.Should().HaveCount(1);
        root.Children[0].Children[0].Heading.Text.Value.Should().Be("Child");
    }

    // -------------------------------------------------------------------------
    // Closing # sequence: CommonMark only strips trailing hashes that are
    // preceded by a space. A # embedded in the text without a leading space
    // is part of the heading text, not a closing sequence.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HeadingWithTrailingHashNotPrecededBySpace_HashIsPartOfText()
    {
        // CommonMark spec: "## foo#" — the # is not a closing sequence because
        // it is not preceded by a space. The heading text should be "foo#".
        const string markdown = "## foo#";

        var model = ParseOk(markdown);

        var section = model.TopLevelSection.Children[0];
        section.Heading.Text.Value.Should().Be("foo#",
            "a trailing # not preceded by whitespace is part of the heading text, not a closing sequence");
    }

    [Test]
    public void Parse_HeadingWithTrailingHashPrecededBySpace_HashIsStripped()
    {
        // "## foo ##" — the ## IS a closing sequence because it is preceded by a space.
        const string markdown = "## foo ##";

        var model = ParseOk(markdown);

        var section = model.TopLevelSection.Children[0];
        section.Heading.Text.Value.Should().Be("foo",
            "a trailing # sequence preceded by whitespace should be stripped");
    }

    // -------------------------------------------------------------------------
    // Heading text with leading/trailing spaces inside the text (not part of trivia)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_HeadingWithLeadingAndTrailingSpacesInText_SpacesAreTrimmed()
    {
        // CommonMark strips leading and trailing spaces from the heading text
        // (but not interior spaces).
        const string markdown = "#   spaces around   ";

        var model = ParseOk(markdown);

        var section = model.TopLevelSection.Children[0];
        section.Heading.Text.Value.Should().Be("spaces around",
            "leading and trailing spaces in the heading text should be stripped");
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
