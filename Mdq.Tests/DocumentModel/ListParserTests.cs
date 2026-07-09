using AwesomeAssertions;
using Mdq.Core.Shared;
using DM = Mdq.Core.DocumentModel;

namespace Mdq.Tests.DocumentModel;

/// <summary>
/// Tests for bulleted and numbered list parsing, item indexing, nesting, and trivia.
/// </summary>
[TestFixture]
public class ListParserTests
{
    // -------------------------------------------------------------------------
    // Bullet characters: -, *, +
    // -------------------------------------------------------------------------

    [TestCase("- Item")]
    [TestCase("* Item")]
    [TestCase("+ Item")]
    public void Parse_BulletedList_AllThreeBulletCharactersRecognized(string markdown)
    {
        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        var list = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>().Subject;
        list.Kind.Should().Be(DM.ListKind.Bulleted);
        list.Items.Should().HaveCount(1);
        list.Items[0].Content.Value.Should().Be("Item");
    }

    // -------------------------------------------------------------------------
    // List item index is 1-based and sequential
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BulletedList_ItemIndicesAreOneBasedAndSequential()
    {
        const string markdown = """
            - Alpha
            - Beta
            - Gamma
            """;

        var model = ParseOk(markdown);
        var list = (DM.ListBlock)model.TopLevelSection.Paragraphs[0];

        list.Items[0].Index.Should().Be(1);
        list.Items[1].Index.Should().Be(2);
        list.Items[2].Index.Should().Be(3);
    }

    [Test]
    public void Parse_NumberedList_ItemIndicesAreOneBasedAndSequential()
    {
        const string markdown = """
            1. First
            2. Second
            3. Third
            """;

        var model = ParseOk(markdown);
        var list = (DM.ListBlock)model.TopLevelSection.Paragraphs[0];

        list.Items[0].Index.Should().Be(1);
        list.Items[1].Index.Should().Be(2);
        list.Items[2].Index.Should().Be(3);
    }

    // -------------------------------------------------------------------------
    // Numbered list: source numbers are non-sequential (e.g. 1, 3, 5)
    // The parser assigns its own sequential index, not the source number.
    // This test documents the current behaviour.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_NumberedList_NonSequentialSourceNumbers_ItemCountIsCorrect()
    {
        const string markdown = """
            1. First
            3. Third
            5. Fifth
            """;

        var model = ParseOk(markdown);
        var list = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>().Subject;

        list.Items.Should().HaveCount(3);
        list.Items[0].Content.Value.Should().Be("First");
        list.Items[1].Content.Value.Should().Be("Third");
        list.Items[2].Content.Value.Should().Be("Fifth");
    }

    // -------------------------------------------------------------------------
    // Two separate lists (blank line between them) become two ListBlock paragraphs
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TwoBulletedListsSeparatedByBlankLine_ProduceTwoListBlocks()
    {
        const string markdown = """
            - Alpha
            - Beta

            - Gamma
            - Delta
            """;

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.ListBlock>();

        var first = (DM.ListBlock)model.TopLevelSection.Paragraphs[0];
        var second = (DM.ListBlock)model.TopLevelSection.Paragraphs[1];
        first.Items.Should().HaveCount(2);
        second.Items.Should().HaveCount(2);
    }

    [Test]
    public void Parse_BulletedListThenNumberedList_ProduceTwoListBlocksWithCorrectKinds()
    {
        const string markdown = """
            - Bullet one
            - Bullet two

            1. Number one
            2. Number two
            """;

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        ((DM.ListBlock)model.TopLevelSection.Paragraphs[0]).Kind.Should().Be(DM.ListKind.Bulleted);
        ((DM.ListBlock)model.TopLevelSection.Paragraphs[1]).Kind.Should().Be(DM.ListKind.Numbered);
    }

    // -------------------------------------------------------------------------
    // A list is terminated by a heading
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ListFollowedByHeading_ListEndsBeforeHeading()
    {
        const string markdown = """
            - Item one
            - Item two
            # Next Section
            """;

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>();
        model.TopLevelSection.Children.Should().HaveCount(1);
        model.TopLevelSection.Children[0].Heading.Text.Value.Should().Be("Next Section");
    }

    // -------------------------------------------------------------------------
    // A list is terminated by a blank line followed by text
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ListFollowedByBlankLineThenText_ProducesListAndTextBlockAsSeparateParagraphs()
    {
        const string markdown = """
            - Item one
            - Item two

            Trailing paragraph.
            """;

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs.Should().HaveCount(2);
        model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>();
        model.TopLevelSection.Paragraphs[1].Should().BeOfType<DM.TextBlock>()
            .Which.Content.Value.Should().Be("Trailing paragraph.");
    }

    // -------------------------------------------------------------------------
    // Sublist: indented bullets nest under the preceding parent item
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_NestedBulletedList_SubListIsOnParentItem()
    {
        const string markdown = """
            - Parent
              - Child A
              - Child B
            """;

        var model = ParseOk(markdown);
        var list = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>().Subject;

        list.Items.Should().HaveCount(1);
        list.Items[0].SubList.Should().NotBeNull();
        list.Items[0].SubList!.Items.Should().HaveCount(2);
        list.Items[0].SubList.Items[0].Content.Value.Should().Be("Child A");
        list.Items[0].SubList.Items[1].Content.Value.Should().Be("Child B");
    }

    [Test]
    public void Parse_NestedList_SubListDoesNotAppearAsTopLevelParagraph()
    {
        const string markdown = """
            - Parent
              - Child
            - Sibling
            """;

        var model = ParseOk(markdown);

        // Everything is one list block; the sublist is not a separate paragraph
        model.TopLevelSection.Paragraphs.Should().HaveCount(1);
        var list = (DM.ListBlock)model.TopLevelSection.Paragraphs[0];
        list.Items.Should().HaveCount(2);
    }

    [Test]
    public void Parse_NestedList_SubListKindMatchesItsOwnMarker()
    {
        const string markdown = """
            - Parent
              - Child
            """;

        var model = ParseOk(markdown);
        var list = (DM.ListBlock)model.TopLevelSection.Paragraphs[0];

        list.Items[0].SubList!.Kind.Should().Be(DM.ListKind.Bulleted);
    }

    [Test]
    public void Parse_NestedNumberedList_SubListIsNumbered()
    {
        const string markdown = """
            1. Parent
               1. Child one
               2. Child two
            2. Sibling
            """;

        var model = ParseOk(markdown);
        var list = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>().Subject;

        list.Items.Should().HaveCount(2);
        list.Items[0].SubList.Should().NotBeNull();
        list.Items[0].SubList!.Kind.Should().Be(DM.ListKind.Numbered);
        list.Items[0].SubList.Items.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // Cross-kind nesting: ordered sublist inside unordered parent (and vice versa)
    // These expose the known gap -- the TODO comment in the parser acknowledges this.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_OrderedSublistInsideUnorderedParent_SubListIsAttachedToParentItem()
    {
        const string markdown = """
            - Parent
              1. Step one
              2. Step two
            - Sibling
            """;

        var model = ParseOk(markdown);
        var list = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>().Subject;

        // The ordered sublist should be attached to the first item, not a separate paragraph
        list.Items.Should().HaveCount(2,
            "the ordered sublist lines should not produce extra top-level items");
        list.Items[0].SubList.Should().NotBeNull(
            "an indented ordered list inside a bulleted item should nest as a sublist");
        list.Items[0].SubList!.Kind.Should().Be(DM.ListKind.Numbered);
        list.Items[0].SubList.Items.Should().HaveCount(2);
    }

    [Test]
    public void Parse_BulletedSublistInsideOrderedParent_SubListIsAttachedToParentItem()
    {
        const string markdown = """
            1. Parent
               - Child A
               - Child B
            2. Sibling
            """;

        var model = ParseOk(markdown);
        var list = model.TopLevelSection.Paragraphs[0].Should().BeOfType<DM.ListBlock>().Subject;

        list.Items.Should().HaveCount(2);
        list.Items[0].SubList.Should().NotBeNull(
            "an indented bulleted list inside a numbered item should nest as a sublist");
        list.Items[0].SubList!.Kind.Should().Be(DM.ListKind.Bulleted);
        list.Items[0].SubList.Items.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // Leading trivia on each list item captures the bullet/number prefix
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BulletedListItem_LeadingTriviaContainsBulletAndSpace()
    {
        const string markdown = "- Item";

        var model = ParseOk(markdown);
        var item = ((DM.ListBlock)model.TopLevelSection.Paragraphs[0]).Items[0];

        item.LeadingTrivia.Value.Should().Be("- ",
            "leading trivia should capture the bullet marker and its trailing space");
    }

    [Test]
    public void Parse_NumberedListItem_LeadingTriviaContainsNumberDotAndSpace()
    {
        const string markdown = "1. Item";

        var model = ParseOk(markdown);
        var item = ((DM.ListBlock)model.TopLevelSection.Paragraphs[0]).Items[0];

        item.LeadingTrivia.Value.Should().Be("1. ",
            "leading trivia should capture the number, dot, and trailing space");
    }

    [Test]
    public void Parse_IndentedBulletedListItem_LeadingTriviaIncludesIndentation()
    {
        const string markdown = """
            - Parent
              - Child
            """;

        var model = ParseOk(markdown);
        var child = ((DM.ListBlock)model.TopLevelSection.Paragraphs[0]).Items[0].SubList!.Items[0];

        child.LeadingTrivia.Value.Should().Be("  - ",
            "leading trivia for an indented item should include the indent spaces and bullet");
    }

    // -------------------------------------------------------------------------
    // Trailing trivia on each list item captures the line ending
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ListItemFollowedByLF_TrailingTriviaIsLineFeed()
    {
        const string markdown = "- Item\n- Next";

        var model = ParseOk(markdown);
        var item = ((DM.ListBlock)model.TopLevelSection.Paragraphs[0]).Items[0];

        item.TrailingTrivia.Value.Should().Be("\n");
    }

    // -------------------------------------------------------------------------
    // Round-trip: LeadingTrivia + Content + TrailingTrivia reproduces the source line
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BulletedList_ItemTriviaAndContentRoundTripsToSourceLine()
    {
        const string sourceLine = "- Hello\n";
        const string markdown = sourceLine + "- World\n";

        var model = ParseOk(markdown);
        var item = ((DM.ListBlock)model.TopLevelSection.Paragraphs[0]).Items[0];

        var reassembled = (item.LeadingTrivia.Value ?? "")
                        + (item.Content.Value ?? "")
                        + (item.TrailingTrivia.Value ?? "");

        reassembled.Should().Be(sourceLine);
    }

    // -------------------------------------------------------------------------
    // Empty list item content (bare bullet with no text)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BulletWithNoContent_ItemContentIsEmpty()
    {
        const string markdown = "- \n- Item";

        var model = ParseOk(markdown);
        var list = (DM.ListBlock)model.TopLevelSection.Paragraphs[0];

        list.Items.Should().HaveCount(2);
        list.Items[0].Content.Value.Should().BeNullOrEmpty();
    }

    // -------------------------------------------------------------------------
    // A bare bullet at end-of-buffer (no trailing newline or space)
    // This exercises the index-out-of-bounds edge case in GetUnorderedListMarker.
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BulletAtEndOfInput_DoesNotThrow()
    {
        // No trailing newline or space after the bullet character.
        // GetUnorderedListMarker checks buffer[index] after the bullet char without
        // guarding against index >= buffer.Length.
        const string markdown = "-";

        var act = () => ParseOk(markdown);

        act.Should().NotThrow("a bare bullet at end-of-input should not cause an index-out-of-bounds exception");
    }

    // -------------------------------------------------------------------------
    // Paragraph index: a list block gets the same sequential paragraph index as a text block
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TextBlockThenList_ListParagraphIndexIsTwo()
    {
        const string markdown = """
            Intro.

            - Item one
            - Item two
            """;

        var model = ParseOk(markdown);

        model.TopLevelSection.Paragraphs[0].Index.Should().Be(1);
        model.TopLevelSection.Paragraphs[1].Index.Should().Be(2);
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
