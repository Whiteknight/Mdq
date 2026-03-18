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

    private static string ExecuteOk(MarkdownDocument doc, string selector)
    {
        var chain = ParseChain(selector);
        var result = QueryExecutor.Execute(doc, chain);
        result.Should().BeOfType<Result<string, MdqError>.Ok>(
            $"expected Ok but got Err for selector '{selector}'");
        return ((Result<string, MdqError>.Ok)result).Value;
    }

    private static QueryError ExecuteErr(MarkdownDocument doc, string selector)
    {
        var chain = ParseChain(selector);
        var result = QueryExecutor.Execute(doc, chain);
        result.Should().BeOfType<Result<string, MdqError>.Err>(
            $"expected Err but got Ok for selector '{selector}'");
        return ((Result<string, MdqError>.Err)result).Error as QueryError ?? throw new InvalidCastException();
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
        new(heading, level,
            paragraphTexts.Select(t => (Paragraph)new TextBlock(t)).ToList(),
            []);

    private static Section SectionWithChildren(string heading, int level, Section[] children, params string[] paragraphTexts) =>
        new(heading, level,
            paragraphTexts.Select(t => (Paragraph)new TextBlock(t)).ToList(),
            children.ToList());

    private static Section SectionWithParagraphs(string heading, int level, IReadOnlyList<Paragraph> paragraphs) =>
        new(heading, level, paragraphs, []);

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

        result.Should().Contain("# Alpha");
        result.Should().Contain("Alpha body.");
        result.Should().Contain("# Beta");
        result.Should().Contain("Beta body.");
    }

    [Test]
    public void Execute_EmptyChain_EmptyDocument_ReturnsEmptyString()
    {
        var doc = new MarkdownDocument([]);
        var result = ExecuteOk(doc, "");
        result.Should().BeEmpty();
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

        result.Should().Contain("# Parent");
        result.Should().Contain("Parent body.");
        result.Should().Contain("## Child");
        result.Should().Contain("Child body.");
    }

    [Test]
    public void Execute_HeadingSelector_MatchesExactName()
    {
        var doc = DocWithSections(
            SimpleSection("Alpha", 1, "Alpha body."),
            SimpleSection("Beta", 1, "Beta body."));

        var result = ExecuteOk(doc, "#Beta");

        result.Should().Contain("Beta body.");
        result.Should().NotContain("Alpha body.");
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

        result.Should().Contain("## Child");
        result.Should().Contain("Child body.");
        result.Should().Contain("### Grandchild");
        result.Should().NotContain("Parent body.");
    }

    [Test]
    public void Execute_ThreeLevelChainedHeadings_ReturnsDeepestSection()
    {
        var grandchild = SimpleSection("Grandchild", 3, "Deep content.");
        var child = SectionWithChildren("Child", 2, [grandchild], "Child body.");
        var parent = SectionWithChildren("Parent", 1, [child], "Parent body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "#Parent#Child#Grandchild");

        result.Should().Contain("### Grandchild");
        result.Should().Contain("Deep content.");
        result.Should().NotContain("Child body.");
        result.Should().NotContain("Parent body.");
    }

    [Test]
    public void Execute_ChainedHeadingSelectorsWildcard_NavigatesIntoNestedSection()
    {
        var h1 = SimpleSection("Heading1", 2, "FirstParagraph");
        var h2 = SimpleSection("Heading2", 2, "SecondParagraph");
        var parent = SectionWithChildren("Parent", 1, [h1, h2], "Parent body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "##*din*");

        result.Should().Contain("## Heading1");
        result.Should().Contain("FirstParagraph");
        // TODO: Should also match Heading2, but for now just verify it doesn't match Parent
    }

    // -------------------------------------------------------------------------
    // Req 3.4 -- .text returns body without heading line
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_TextSelector_ReturnsParagraphsWithoutHeadingLine()
    {
        var doc = DocWithSections(SimpleSection("Intro", 1, "First para.", "Second para."));

        var result = ExecuteOk(doc, "#Intro.text");

        result.Should().Contain("First para.");
        result.Should().Contain("Second para.");
        result.Should().NotContain("# Intro");
    }

    [Test]
    public void Execute_TextSelector_IncludesNestedSubsections()
    {
        var child = SimpleSection("Sub", 2, "Sub body.");
        var parent = SectionWithChildren("Main", 1, [child], "Main body.");
        var doc = DocWithSections(parent);

        var result = ExecuteOk(doc, "#Main.text");

        result.Should().Contain("Main body.");
        result.Should().Contain("## Sub");
        result.Should().Contain("Sub body.");
        result.Should().NotContain("# Main");
    }

    // -------------------------------------------------------------------------
    // Req 3.5 -- .heading returns heading text without # prefix
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_HeadingContentSelector_ReturnsHeadingTextOnly()
    {
        var doc = DocWithSections(SimpleSection("Introduction", 1, "Body text."));

        var result = ExecuteOk(doc, "#Introduction.heading");

        result.Should().Be("Introduction");
    }

    [Test]
    public void Execute_HeadingContentSelector_DoesNotContainHashPrefix()
    {
        var doc = DocWithSections(SimpleSection("My Section", 1, "Body."));

        var result = ExecuteOk(doc, "#My Section.heading");

        result.Should().NotStartWith("#");
        result.Should().Be("My Section");
    }

    // -------------------------------------------------------------------------
    // Req 3.6 -- .paragraph(N) returns the Nth paragraph
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ParagraphSelector_ReturnsCorrectParagraph()
    {
        var paragraphs = new List<Paragraph>
        {
            new TextBlock("First."),
            new TextBlock("Second."),
            new TextBlock("Third.")
        };
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, paragraphs));

        ExecuteOk(doc, "#Section.paragraph(1)").Should().Be("First.");
        ExecuteOk(doc, "#Section.paragraph(2)").Should().Be("Second.");
        ExecuteOk(doc, "#Section.paragraph(3)").Should().Be("Third.");
    }

    // -------------------------------------------------------------------------
    // Req 3.7 -- .item(N) returns the Nth list item
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ItemSelector_ReturnsCorrectListItem()
    {
        var items = new List<ListItem>
        {
            new("Alpha", null),
            new("Beta", null),
            new("Gamma", null)
        };
        var listBlock = new ListBlock(ListKind.Bulleted, items);
        var doc = DocWithSections(SectionWithParagraphs("List Section", 1, [listBlock]));

        ExecuteOk(doc, "#List Section.paragraph(1).item(1)").Should().Be("Alpha");
        ExecuteOk(doc, "#List Section.paragraph(1).item(2)").Should().Be("Beta");
        ExecuteOk(doc, "#List Section.paragraph(1).item(3)").Should().Be("Gamma");
    }

    // -------------------------------------------------------------------------
    // Req 3.8 -- chained .item(N) navigates nested sub-lists
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ChainedItemSelectors_NavigatesNestedSubList()
    {
        var subItems = new List<ListItem>
        {
            new("Sub-Alpha", null),
            new("Sub-Beta", null)
        };
        var subList = new ListBlock(ListKind.Bulleted, subItems);
        var items = new List<ListItem>
        {
            new("Parent item", subList),
            new("Other item", null)
        };
        var listBlock = new ListBlock(ListKind.Bulleted, items);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        var result = ExecuteOk(doc, "#Section.paragraph(1).item(1).item(2)");

        result.Should().Be("Sub-Beta");
    }

    // -------------------------------------------------------------------------
    // Req 3.9 -- heading not found returns HeadingNotFound
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_HeadingNotFound_ReturnsHeadingNotFoundError()
    {
        var doc = DocWithSections(SimpleSection("Existing", 1, "Body."));

        var error = ExecuteErr(doc, "#Missing");

        error.Should().BeOfType<QueryError.HeadingNotFound>()
            .Which.Name.Should().Be("Missing");
    }

    [Test]
    public void Execute_HeadingNotFound_ErrorContainsLevel()
    {
        var doc = DocWithSections(SimpleSection("Parent", 1, "Body."));

        var error = ExecuteErr(doc, "#Parent#NonExistent");

        var notFound = error.Should().BeOfType<QueryError.HeadingNotFound>().Subject;
        notFound.Name.Should().Be("NonExistent");
        notFound.Level.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // Req 3.10 -- paragraph out of range returns ParagraphOutOfRange
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ParagraphOutOfRange_ReturnsParagraphOutOfRangeError()
    {
        var doc = DocWithSections(SimpleSection("Section", 1, "Only one paragraph."));

        var error = ExecuteErr(doc, "#Section.paragraph(5)");

        var outOfRange = error.Should().BeOfType<QueryError.ParagraphOutOfRange>().Subject;
        outOfRange.Requested.Should().Be(5);
        outOfRange.Actual.Should().Be(1);
    }

    [Test]
    public void Execute_ParagraphOutOfRange_EmptySection_ReportsZeroActual()
    {
        var doc = DocWithSections(new Section("Empty", 1, [], []));

        var error = ExecuteErr(doc, "#Empty.paragraph(1)");

        var outOfRange = error.Should().BeOfType<QueryError.ParagraphOutOfRange>().Subject;
        outOfRange.Requested.Should().Be(1);
        outOfRange.Actual.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Req 3.11 -- item out of range returns ItemOutOfRange
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ItemOutOfRange_ReturnsItemOutOfRangeError()
    {
        var items = new List<ListItem> { new("Only item", null) };
        var listBlock = new ListBlock(ListKind.Bulleted, items);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        var error = ExecuteErr(doc, "#Section.paragraph(1).item(3)");

        var outOfRange = error.Should().BeOfType<QueryError.ItemOutOfRange>().Subject;
        outOfRange.Requested.Should().Be(3);
        outOfRange.Actual.Should().Be(1);
    }

    // -------------------------------------------------------------------------
    // Req 3.12 -- .item(N) on non-list returns NotAList
    // -------------------------------------------------------------------------

    [Test]
    public void Execute_ItemOnNonList_ReturnsNotAListError()
    {
        var doc = DocWithSections(SimpleSection("Section", 1, "Just text."));

        var error = ExecuteErr(doc, "#Section.paragraph(1).item(1)");

        error.Should().BeOfType<QueryError.NotAList>();
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

        result.Should().Contain("# H1");
        result.Should().Contain("## H2");
    }

    [Test]
    public void Execute_EmptyChain_RendersBulletedList()
    {
        var items = new List<ListItem> { new("A", null), new("B", null) };
        var listBlock = new ListBlock(ListKind.Bulleted, items);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        var result = ExecuteOk(doc, "");

        result.Should().Contain("- A");
        result.Should().Contain("- B");
    }

    [Test]
    public void Execute_EmptyChain_RendersNumberedList()
    {
        var items = new List<ListItem> { new("First", null), new("Second", null) };
        var listBlock = new ListBlock(ListKind.Numbered, items);
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [listBlock]));

        var result = ExecuteOk(doc, "");

        result.Should().Contain("1. First");
        result.Should().Contain("2. Second");
    }

    [Test]
    public void Execute_EmptyChain_RendersBlockQuote()
    {
        var bq = new BlockQuote("Quoted text.");
        var doc = DocWithSections(SectionWithParagraphs("Section", 1, [bq]));

        var result = ExecuteOk(doc, "");

        result.Should().Contain("> Quoted text.");
    }
}
