using AwesomeAssertions;
using Mdq.Core.DocumentModel;
using Mdq.Core.QueryEngine;
using Mdq.Core.SelectorModel;
using Mdq.Core.Shared;

namespace Mdq.Tests.QueryEngine;

[TestFixture]
public class QueryExecutorTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static List<MatchableItem> ExecuteOk(MarkdownDocument doc, string selector)
    {
        var chain = ParseChain(selector);
        var result = QueryExecutor.Execute(doc, chain);
        result.IsSuccess.Should().BeTrue($"expected Ok but got Err for selector '{selector}'");
        return result.GetValueOrDefault();
    }

    private static void ExecuteOkEmpty(MarkdownDocument doc, string selector)
    {
        var chain = ParseChain(selector);
        var result = QueryExecutor.Execute(doc, chain);
        result.IsSuccess.Should().BeTrue($"expected Ok but got Err for selector '{selector}'");
        result.GetValueOrDefault([]).Should().BeEmpty();
    }

    private static MdqError ExecuteErr(MarkdownDocument doc, string selector)
    {
        var chain = ParseChain(selector);
        var result = QueryExecutor.Execute(doc, chain);
        result.IsError.Should().BeTrue($"expected Err but got Ok for selector '{selector}'");
        return result.GetErrorOrDefault();
    }

    private static SelectorChain ParseChain(string selector)
    {
        var result = SelectorParser.Parse(selector);
        result.Should().BeOfType<Result<SelectorChain, MdqError>.Ok>(
            $"selector '{selector}' should parse without error");
        return ((Result<SelectorChain, MdqError>.Ok)result).Value;
    }

    // -------------------------------------------------------------------------
    // Document builders
    // -------------------------------------------------------------------------

    private static MarkdownDocument DocWithSections(params Section[] sections) =>
        new(sections.ToList());

    private static Section SimpleSection(string heading, int level, params string[] paragraphTexts) =>
        new(new Heading(heading, level),
            paragraphTexts.Select(t => (Paragraph)new TextBlock(t, 1)).ToList(),
            []);

    private static Section SectionWithChildren(string heading, int level, Section[] children, params string[] paragraphTexts) =>
        new(new Heading(heading, level),
            paragraphTexts.Select(t => (Paragraph)new TextBlock(t, 1)).ToList(),
            children.ToList());

    private static Section SectionWithParagraphs(string heading, int level, IReadOnlyList<Paragraph> paragraphs) =>
        new(new Heading(heading, level), paragraphs, []);

    // -------------------------------------------------------------------------
    // Req 3.1 -- empty chain returns full document content
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_EmptyChain_ReturnsFullDocumentContent()
    {
        var doc = DocWithSections(
            SimpleSection("Alpha", 1, "Alpha body."),
            SimpleSection("Beta", 1, "Beta body."));

        var result = ExecuteOk(doc, "");

        var resultDoc = result[0].Should().BeOfType<MarkdownDocument>().Which;
        resultDoc.Sections[0].Heading.Text.Should().Be("Alpha");
        resultDoc.Sections[1].Heading.Text.Should().Be("Beta");
    }

    [Test]
    public void Execute_EmptyChain_EmptyDocument_ReturnsEmptyString()
    {
        var doc = new MarkdownDocument([]);
        var result = ExecuteOk(doc, "");
        result[0].Should().Be(doc);
    }

    // -------------------------------------------------------------------------
    // Req 3.2 -- single heading selector returns section with heading + body + children
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_SingleHeadingSelector_ReturnsMatchedSection()
    {
        var child = SimpleSection("Child", 2, "Child body.");
        var parent = SectionWithChildren("Parent", 1, [child], "Parent body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "#Parent");

        var section = result[0].Should().BeOfType<Section>().Which;
        section.Heading.Text.Should().Be("Parent");

        section.Paragraphs.Should().ContainSingle()
            .Which.Should().BeOfType<TextBlock>()
            .Which.Content.Should().Be("Parent body.");

        section.Children.Should().ContainSingle()
            .Which.Should().BeOfType<Section>()
            .Which.Heading.Text.Should().Be("Child");
    }

    [Test]
    public void Execute_HeadingSelector_MatchesExactName()
    {
        var doc = DocWithSections(
            SimpleSection("Alpha", 1, "Alpha body."),
            SimpleSection("Beta", 1, "Beta body."));

        var result = ExecuteOk(doc, "#Beta");

        result[0].Should().BeOfType<Section>()
            .Which.Heading.Text.Should().Be("Beta");
    }

    // -------------------------------------------------------------------------
    // Req 3.3 -- chained heading selectors navigate nested sections
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ChainedHeadingSelectors_NavigatesIntoNestedSection()
    {
        var grandchild = SimpleSection("Grandchild", 3, "Deep content.");
        var child = SectionWithChildren("Child", 2, [grandchild], "Child body.");
        var parent = SectionWithChildren("Parent", 1, [child], "Parent body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "#Parent#Child");

        var section = result[0].Should().BeOfType<Section>().Which;
        section.Heading.Text.Should().Be("Child");

        section.Paragraphs.Should().ContainSingle()
            .Which.Should().BeOfType<TextBlock>()
            .Which.Content.Should().Be("Child body.");

        section.Children.Should().ContainSingle()
            .Which.Should().BeOfType<Section>()
            .Which.Heading.Text.Should().Be("Grandchild");
    }

    [Test]
    public void Execute_ThreeLevelChainedHeadings_ReturnsDeepestSection()
    {
        var grandchild = SimpleSection("Grandchild", 3, "Deep content.");
        var child = SectionWithChildren("Child", 2, [grandchild], "Child body.");
        var parent = SectionWithChildren("Parent", 1, [child], "Parent body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "#Parent#Child#Grandchild");

        result[0].Should().BeOfType<Section>()
            .Which.Heading.Text.Should().Be("Grandchild");
    }

    [Test]
    public void Execute_ChainedHeadingSelectorsWildcard_NavigatesIntoNestedSection()
    {
        var h1 = SimpleSection("Heading1", 2, "FirstParagraph");
        var h2 = SimpleSection("Heading2", 2, "SecondParagraph");
        var parent = SectionWithChildren("Parent", 1, [h1, h2], "Parent body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "##*din*");

        result[0].Should().BeOfType<Section>()
            .Which.Heading.Text.Should().Be("Heading1");
        result[1].Should().BeOfType<Section>()
            .Which.Heading.Text.Should().Be("Heading2");
    }

    // -------------------------------------------------------------------------
    // Req 3.4 -- .text returns body without heading line
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_TextSelector_ReturnsParagraphsWithoutHeadingLine()
    {
        var doc = DocWithSections(SimpleSection("Intro", 1, "First para.", "Second para."));

        var result = ExecuteOk(doc, "#Intro.text");

        result[0].Should().BeOfType<TextBlock>().Which.Content.Should().Contain("First para.");
        result[1].Should().BeOfType<TextBlock>().Which.Content.Should().Contain("Second para.");
    }

    [Test]
    public void Execute_TextSelector_IncludesNestedSubsections()
    {
        var child = SimpleSection("Sub", 2, "Sub body.");
        var parent = SectionWithChildren("Main", 1, [child], "Main body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "#Main.text");

        result[0].Should().BeOfType<TextBlock>()
            .Which.Content.Should().Contain("Main body.");
    }

    // -------------------------------------------------------------------------
    // Req 3.5 -- .heading returns heading text without # prefix
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_HeadingContentSelector_ReturnsHeadingTextOnly()
    {
        var doc = DocWithSections(SimpleSection("Introduction", 1, "Body text."));

        var result = ExecuteOk(doc, "#Introduction.heading");

        result[0].Should().BeOfType<Heading>()
            .Which.Text.Should().Be("Introduction");
    }

    [Test]
    public void Execute_HeadingContentSelector_DoesNotContainHashPrefix()
    {
        var doc = DocWithSections(SimpleSection("My Section", 1, "Body."));

        var result = ExecuteOk(doc, "#My Section.heading");

        result[0].Should().BeOfType<Heading>()
            .Which.Text.Should().Be("My Section");
    }

    // -------------------------------------------------------------------------
    // Req 3.6 -- .paragraph(N) returns the Nth paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ParagraphSelector_ReturnsCorrectParagraph()
    {
        var paragraphs = new List<Paragraph>
        {
            new TextBlock("First.", 1),
            new TextBlock("Second.", 2),
            new TextBlock("Third.", 3)
        };
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, paragraphs));

        ExecuteOk(doc, "#Section.paragraph(1)")[0].Should().BeOfType<TextBlock>().Which.Content.Should().Be("First.");
        ExecuteOk(doc, "#Section.paragraph(2)")[0].Should().BeOfType<TextBlock>().Which.Content.Should().Be("Second.");
        ExecuteOk(doc, "#Section.paragraph(3)")[0].Should().BeOfType<TextBlock>().Which.Content.Should().Be("Third.");
    }

    // -------------------------------------------------------------------------
    // Req 3.7 -- .item(N) returns the Nth list item
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ItemSelector_ReturnsCorrectListItem()
    {
        var items = new List<ListItem>
        {
            new("Alpha", ListKind.Bulleted, 1, null),
            new("Beta", ListKind.Bulleted, 2, null),
            new("Gamma", ListKind.Bulleted, 3, null)
        };
        var listBlock = new ListBlock(ListKind.Bulleted, items, 1);
        var doc = DocWithSections(SectionWithParagraphs("List Section", 1, [listBlock]));

        ExecuteOk(doc, "#List Section.paragraph(1).item(1)")[0].Should().BeOfType<ListItem>().Which.Content.Should().Be("Alpha");
        ExecuteOk(doc, "#List Section.paragraph(1).item(2)")[0].Should().BeOfType<ListItem>().Which.Content.Should().Be("Beta");
        ExecuteOk(doc, "#List Section.paragraph(1).item(3)")[0].Should().BeOfType<ListItem>().Which.Content.Should().Be("Gamma");
    }

    // -------------------------------------------------------------------------
    // Req 3.8 -- chained .item(N) navigates nested sub-lists
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ChainedItemSelectors_NavigatesNestedSubList()
    {
        var subItems = new List<ListItem>
        {
            new("Sub-Alpha", ListKind.Bulleted, 1, null),
            new("Sub-Beta", ListKind.Bulleted, 2, null)
        };
        var subList = new ListBlock(ListKind.Bulleted, subItems, 1);
        var items = new List<ListItem>
        {
            new("Parent item", ListKind.Bulleted, 1, subList),
            new("Other item", ListKind.Bulleted, 2, null)
        };
        var listBlock = new ListBlock(ListKind.Bulleted, items, 1);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        var result = ExecuteOk(doc, "#Section.paragraph(1).item(1).item(2)");

        result[0].Should().BeOfType<ListItem>().Which.Content.Should().Be("Sub-Beta");
    }

    // -------------------------------------------------------------------------
    // Req 3.9 -- heading not found returns HeadingNotFound
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_HeadingNotFound_ReturnsHeadingNotFoundError()
    {
        var doc = DocWithSections(SimpleSection("Existing", 1, "Body."));

        ExecuteOkEmpty(doc, "#Missing");
    }

    [Test]
    public void Execute_HeadingNotFound_ErrorContainsLevel()
    {
        var doc = DocWithSections(SimpleSection("Parent", 1, "Body."));

        ExecuteOkEmpty(doc, "#Parent#NonExistent");
    }

    // -------------------------------------------------------------------------
    // Req 3.10 -- paragraph out of range returns ParagraphOutOfRange
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ParagraphOutOfRange_ReturnsParagraphOutOfRangeError()
    {
        var doc = DocWithSections(SimpleSection("Section", 1, "Only one paragraph."));

        ExecuteOkEmpty(doc, "#Section.paragraph(5)");
    }

    [Test]
    public void Execute_ParagraphOutOfRange_EmptySection_ReportsZeroActual()
    {
        var doc = DocWithSections(new Section(new Heading("Empty", 1), [], []));

        ExecuteOkEmpty(doc, "#Empty.paragraph(1)");
    }

    // -------------------------------------------------------------------------
    // Req 3.11 -- item out of range returns ItemOutOfRange
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ItemOutOfRange_ReturnsItemOutOfRangeError()
    {
        var items = new List<ListItem> { new("Only item", ListKind.Bulleted, 1, null) };
        var listBlock = new ListBlock(ListKind.Bulleted, items, 1);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        ExecuteOkEmpty(doc, "#Section.paragraph(1).item(3)");
    }

    // -------------------------------------------------------------------------
    // Req 3.12 -- .item(N) on non-list returns NotAList
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ItemOnNonList_ReturnsNotAListError()
    {
        var doc = DocWithSections(SimpleSection("Section", 1, "Just text."));

        ExecuteOkEmpty(doc, "#Section.paragraph(1).item(1)");
    }

    // -------------------------------------------------------------------------
    // Rendering correctness
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_EmptyChain_RendersHeadingWithCorrectHashCount()
    {
        var doc = DocWithSections(
            SectionWithChildren("H1", 1,
                [SimpleSection("H2", 2, "H2 body.")],
                "H1 body."));

        var result = ExecuteOk(doc, "");

        result[0].Should().BeOfType<MarkdownDocument>()
            .Which.Sections.Should().ContainSingle()
            .Which.Heading.Text.Should().Be("H1");
    }

    [Test]
    public void Execute_EmptyChain_RendersBulletedList()
    {
        var items = new List<ListItem> { new("A", ListKind.Bulleted, 1, null), new("B", ListKind.Bulleted, 2, null) };
        var listBlock = new ListBlock(ListKind.Bulleted, items, 1);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        var result = ExecuteOk(doc, "");

        result[0].Should().BeOfType<MarkdownDocument>()
            .Which.Sections.Should().ContainSingle()
            .Which.Paragraphs.Should().ContainSingle()
            .Which.Should().BeOfType<ListBlock>()
            .Which.Items.Should().ContainInOrder(items);
    }

    [Test]
    public void Execute_EmptyChain_RendersNumberedList()
    {
        var items = new List<ListItem> { new("First", ListKind.Numbered, 1, null), new("Second", ListKind.Numbered, 2, null) };
        var listBlock = new ListBlock(ListKind.Numbered, items, 1);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        var result = ExecuteOk(doc, "");

        result[0].Should().BeOfType<MarkdownDocument>()
            .Which.Sections.Should().ContainSingle()
            .Which.Paragraphs.Should().ContainSingle()
            .Which.Should().BeOfType<ListBlock>()
            .Which.Items.Should().ContainInOrder(items);
    }

    [Test]
    public void Execute_EmptyChain_RendersBlockQuote()
    {
        var bq = new BlockQuote("Quoted text.", 1);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [bq]));

        var result = ExecuteOk(doc, "");

        result.Should().ContainSingle()
            .Which.Should().BeOfType<MarkdownDocument>()
            .Which.Sections.Should().ContainSingle()
            .Which.Should().BeOfType<Section>()
            .Which.Paragraphs.Should().ContainSingle()
            .Which.Should().BeOfType<BlockQuote>()
            .Which.Content.Should().Be("Quoted text.");
    }
}
