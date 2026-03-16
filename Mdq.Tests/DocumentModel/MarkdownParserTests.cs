using AwesomeAssertions;
using Mdq.Core.Shared;
using DM = Mdq.Core.DocumentModel;

namespace Mdq.Tests.DocumentModel;

[TestFixture]
public class MarkdownParserTests
{
    // -------------------------------------------------------------------------
    // Req 1.7 -- empty document
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_EmptyString_ReturnsEmptyModel()
    {
        var result = DM.MarkdownParser.Parse(string.Empty);

        result.Should().BeOfType<Result<DM.MarkdownDocument, MdqError>.Ok>();
        var ok = (Result<DM.MarkdownDocument, MdqError>.Ok)result;
        ok.Value.Sections.Should().BeEmpty();
    }

    [Test]
    public void Parse_WhitespaceOnly_ReturnsEmptyModel()
    {
        var result = DM.MarkdownParser.Parse("   \n\n   ");

        result.Should().BeOfType<Result<DM.MarkdownDocument, MdqError>.Ok>();
        var ok = (Result<DM.MarkdownDocument, MdqError>.Ok)result;
        ok.Value.Sections.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Req 1.2 -- preamble (content before first heading)
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_ContentBeforeFirstHeading_StoresAsPreambleSection()
    {
        const string markdown = """
            Some introductory text.

            # First Heading
            """;

        var model = ParseOk(markdown);

        model.Sections.Should().HaveCount(2);
        var preamble = model.Sections[0];
        preamble.HeadingText.Should().BeNull();
        preamble.HeadingLevel.Should().Be(0);
        preamble.Paragraphs.Should().HaveCount(1);
        preamble.Paragraphs[0].Should().BeOfType<DM.Paragraph.TextBlock>()
            .Which.Content.Should().Be("Some introductory text.");
    }

    [Test]
    public void Parse_NoContentBeforeFirstHeading_NoPreambleSection()
    {
        const string markdown = """
            # First Heading
            Body text.
            """;

        var model = ParseOk(markdown);

        model.Sections.Should().HaveCount(1);
        model.Sections[0].HeadingText.Should().Be("First Heading");
    }

    // -------------------------------------------------------------------------
    // Req 1.1 -- heading hierarchy / section tree
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_SingleH1_ProducesOneSectionAtLevelOne()
    {
        const string markdown = """
            # Alpha
            Alpha body.
            """;

        var model = ParseOk(markdown);

        model.Sections.Should().HaveCount(1);
        var section = model.Sections[0];
        section.HeadingText.Should().Be("Alpha");
        section.HeadingLevel.Should().Be(1);
        section.Children.Should().BeEmpty();
    }

    [Test]
    public void Parse_H1WithH2Child_BuildsCorrectTree()
    {
        const string markdown = """
            # Parent
            Parent body.

            ## Child
            Child body.
            """;

        var model = ParseOk(markdown);

        model.Sections.Should().HaveCount(1);
        var parent = model.Sections[0];
        parent.HeadingText.Should().Be("Parent");
        parent.HeadingLevel.Should().Be(1);
        parent.Children.Should().HaveCount(1);

        var child = parent.Children[0];
        child.HeadingText.Should().Be("Child");
        child.HeadingLevel.Should().Be(2);
        child.Children.Should().BeEmpty();
    }

    [Test]
    public void Parse_TwoSiblingH1s_ProducesTwoRootSections()
    {
        const string markdown = """
            # Alpha
            Alpha body.

            # Beta
            Beta body.
            """;

        var model = ParseOk(markdown);

        model.Sections.Should().HaveCount(2);
        model.Sections[0].HeadingText.Should().Be("Alpha");
        model.Sections[1].HeadingText.Should().Be("Beta");
    }

    [Test]
    public void Parse_H1WithTwoH2Children_BothChildrenNested()
    {
        const string markdown = """
            # Root
            Root body.

            ## Child A
            A body.

            ## Child B
            B body.
            """;

        var model = ParseOk(markdown);

        model.Sections.Should().HaveCount(1);
        var root = model.Sections[0];
        root.Children.Should().HaveCount(2);
        root.Children[0].HeadingText.Should().Be("Child A");
        root.Children[1].HeadingText.Should().Be("Child B");
    }

    [Test]
    public void Parse_ThreeLevelHierarchy_CorrectlyNested()
    {
        const string markdown = """
            # H1
            ## H2
            ### H3
            Deep content.
            """;

        var model = ParseOk(markdown);

        model.Sections.Should().HaveCount(1);
        var h1 = model.Sections[0];
        h1.HeadingLevel.Should().Be(1);
        h1.Children.Should().HaveCount(1);

        var h2 = h1.Children[0];
        h2.HeadingLevel.Should().Be(2);
        h2.Children.Should().HaveCount(1);

        var h3 = h2.Children[0];
        h3.HeadingLevel.Should().Be(3);
        h3.Children.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------
    // Req 1.3 -- blank-line-separated text blocks become distinct paragraphs
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_TwoTextBlocksSeparatedByBlankLine_ProducesTwoParagraphs()
    {
        const string markdown = """
            # Section

            First block.

            Second block.
            """;

        var model = ParseOk(markdown);
        var section = model.Sections[0];

        section.Paragraphs.Should().HaveCount(2);
        section.Paragraphs[0].Should().BeOfType<DM.Paragraph.TextBlock>()
            .Which.Content.Should().Be("First block.");
        section.Paragraphs[1].Should().BeOfType<DM.Paragraph.TextBlock>()
            .Which.Content.Should().Be("Second block.");
    }

    [Test]
    public void Parse_ThreeTextBlocks_ProducesThreeParagraphs()
    {
        const string markdown = """
            # Section

            One.

            Two.

            Three.
            """;

        var model = ParseOk(markdown);
        model.Sections[0].Paragraphs.Should().HaveCount(3);
    }

    // -------------------------------------------------------------------------
    // Req 1.4 -- bulleted and numbered lists as single ListBlock
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BulletedList_ProducesSingleListBlockWithCorrectItems()
    {
        const string markdown = """
            # Section
            - Alpha
            - Beta
            - Gamma
            """;

        var model = ParseOk(markdown);
        var section = model.Sections[0];

        section.Paragraphs.Should().HaveCount(1);
        var listBlock = section.Paragraphs[0].Should().BeOfType<DM.Paragraph.ListBlock>().Subject;
        listBlock.Kind.Should().Be(DM.ListKind.Bulleted);
        listBlock.Items.Should().HaveCount(3);
        listBlock.Items[0].Content.Should().Be("Alpha");
        listBlock.Items[1].Content.Should().Be("Beta");
        listBlock.Items[2].Content.Should().Be("Gamma");
    }

    [Test]
    public void Parse_NumberedList_ProducesSingleListBlockWithNumberedKind()
    {
        const string markdown = """
            # Section
            1. First
            2. Second
            3. Third
            """;

        var model = ParseOk(markdown);
        var listBlock = model.Sections[0].Paragraphs[0].Should().BeOfType<DM.Paragraph.ListBlock>().Subject;
        listBlock.Kind.Should().Be(DM.ListKind.Numbered);
        listBlock.Items.Should().HaveCount(3);
        listBlock.Items[0].Content.Should().Be("First");
        listBlock.Items[1].Content.Should().Be("Second");
        listBlock.Items[2].Content.Should().Be("Third");
    }

    // -------------------------------------------------------------------------
    // Req 1.5 -- nested sub-lists populate ListItem.SubList
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_NestedSubList_PopulatesListItemSubList()
    {
        const string markdown = """
            # Section
            - Parent item
              - Child one
              - Child two
            - Another parent
            """;

        var model = ParseOk(markdown);
        var listBlock = model.Sections[0].Paragraphs[0].Should().BeOfType<DM.Paragraph.ListBlock>().Subject;

        listBlock.Items.Should().HaveCount(2);

        var parentItem = listBlock.Items[0];
        parentItem.Content.Should().Be("Parent item");
        parentItem.SubList.Should().NotBeNull();
        parentItem.SubList!.Items.Should().HaveCount(2);
        parentItem.SubList.Items[0].Content.Should().Be("Child one");
        parentItem.SubList.Items[1].Content.Should().Be("Child two");

        listBlock.Items[1].SubList.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // Req 1.6 -- block quotes parsed as BlockQuote paragraphs
    // -------------------------------------------------------------------------

    [Test]
    public void Parse_BlockQuote_ProducesBlockQuoteParagraph()
    {
        const string markdown = """
            # Section
            > This is a quote.
            """;

        var model = ParseOk(markdown);
        var section = model.Sections[0];

        section.Paragraphs.Should().HaveCount(1);
        section.Paragraphs[0].Should().BeOfType<DM.Paragraph.BlockQuote>()
            .Which.Content.Should().Be("This is a quote.");
    }

    [Test]
    public void Parse_BlockQuoteAmongOtherParagraphs_CorrectlyIdentified()
    {
        const string markdown = """
            # Section
            Before quote.

            > Quoted text.

            After quote.
            """;

        var model = ParseOk(markdown);
        var paragraphs = model.Sections[0].Paragraphs;

        paragraphs.Should().HaveCount(3);
        paragraphs[0].Should().BeOfType<DM.Paragraph.TextBlock>();
        paragraphs[1].Should().BeOfType<DM.Paragraph.BlockQuote>();
        paragraphs[2].Should().BeOfType<DM.Paragraph.TextBlock>();
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
