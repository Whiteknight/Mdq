using AwesomeAssertions;
using Mdq.Core.DocumentModel;
using Mdq.Core.Editing;
using Mdq.Core.Rendering;
using Mdq.Core.Shared;

namespace Mdq.Tests.Rendering;

[TestFixture]
public class EditingMarkdownRendererTests
{
    // -------------------------------------------------------------------------
    // Helpers -- document builders
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a document with a single section containing the given paragraphs.
    /// Returns both the document and the direct references to the paragraph nodes
    /// so tests can pass them as targets.
    /// </summary>
    private static (MarkdownDocument Doc, T Target) DocWith<T>(T paragraph)
        where T : Paragraph
    {
        var section = new Section(new Heading("Section", 1), [paragraph], []);
        var doc = new MarkdownDocument([section]);
        return (doc, paragraph);
    }

    /// <summary>
    /// Builds a document with two sections so we can verify sibling sections
    /// are rendered unchanged.
    /// </summary>
    private static (MarkdownDocument Doc, Section TargetSection, Section SiblingSection) DocWithTwoSections()
    {
        var sibling = new Section(new Heading("Sibling", 1), [new TextBlock("sibling body", 1)], []);
        var target = new Section(new Heading("Target", 1), [new TextBlock("target body", 1)], []);
        var doc = new MarkdownDocument([sibling, target]);
        return (doc, target, sibling);
    }

    private static string Render(MarkdownDocument doc, MatchableItem target, EditOperation op)
        => new EditingMarkdownRenderer(target, op).Render(doc);

    private static MarkdownDocument ParseOk(string markdown)
    {
        var result = MarkdownParser.Parse(markdown);
        result.IsSuccess.Should().BeTrue("parse should succeed");
        return result.GetValueOrDefault();
    }

    // -------------------------------------------------------------------------
    // Add -- TextBlock
    // -------------------------------------------------------------------------

    [Test]
    public void Add_TextBlock_AppendsTextWithSpaceSeparator()
    {
        var (doc, tb) = DocWith(new TextBlock("Hello", 1));

        var output = Render(doc, tb, new Add("world"));

        output.Should().Contain("Hello world");
    }

    [Test]
    public void Add_TextBlock_OriginalContentIsPreserved()
    {
        var (doc, tb) = DocWith(new TextBlock("Original", 1));

        var output = Render(doc, tb, new Add("appended"));

        output.Should().Contain("Original appended");
        output.Should().NotContain("Original\n");
    }

    // -------------------------------------------------------------------------
    // Set -- TextBlock
    // -------------------------------------------------------------------------

    [Test]
    public void Set_TextBlock_ReplacesContent()
    {
        var (doc, tb) = DocWith(new TextBlock("old content", 1));

        var output = Render(doc, tb, new Set("new content"));

        output.Should().Contain("new content");
        output.Should().NotContain("old content");
    }

    // -------------------------------------------------------------------------
    // Add -- ListBlock
    // -------------------------------------------------------------------------

    [Test]
    public void Add_ListBlock_AppendsBulletedItem()
    {
        var items = new List<ListItem>
        {
            new("first", ListKind.Bulleted, 1, null),
            new("second", ListKind.Bulleted, 2, null),
        };
        var lb = new ListBlock(ListKind.Bulleted, items, 1);
        var (doc, target) = DocWith(lb);

        var output = Render(doc, target, new Add("third"));

        output.Should().Contain("- first");
        output.Should().Contain("- second");
        output.Should().Contain("- third");
    }

    [Test]
    public void Add_ListBlock_NewItemIndexIsCountPlusOne()
    {
        var items = new List<ListItem>
        {
            new("a", ListKind.Numbered, 1, null),
            new("b", ListKind.Numbered, 2, null),
        };
        var lb = new ListBlock(ListKind.Numbered, items, 1);
        var (doc, target) = DocWith(lb);

        var output = Render(doc, target, new Add("c"));

        // Numbered list: new item should be "3. c"
        output.Should().Contain("3. c");
    }

    [Test]
    public void Add_ListBlock_NewItemInheritsListKind_Bulleted()
    {
        var lb = new ListBlock(ListKind.Bulleted, [new ListItem("x", ListKind.Bulleted, 1, null)], 1);
        var (doc, target) = DocWith(lb);

        var output = Render(doc, target, new Add("y"));

        output.Should().Contain("- y");
        output.Should().NotContain("2. y");
    }

    [Test]
    public void Add_ListBlock_NewItemInheritsListKind_Numbered()
    {
        var lb = new ListBlock(ListKind.Numbered, [new ListItem("x", ListKind.Numbered, 1, null)], 1);
        var (doc, target) = DocWith(lb);

        var output = Render(doc, target, new Add("y"));

        output.Should().Contain("2. y");
        output.Should().NotContain("- y");
    }

    [Test]
    public void Add_ListBlock_ExistingItemsAreUnchanged()
    {
        var lb = new ListBlock(ListKind.Bulleted, [new ListItem("original", ListKind.Bulleted, 1, null)], 1);
        var (doc, target) = DocWith(lb);

        var output = Render(doc, target, new Add("new"));

        output.Should().Contain("- original");
    }

    // -------------------------------------------------------------------------
    // Add -- CodeBlock
    // -------------------------------------------------------------------------

    [Test]
    public void Add_CodeBlock_AppendsLineWithNewlineSeparator()
    {
        var cb = new CodeBlock("csharp", "int x = 1;", 1);
        var (doc, target) = DocWith(cb);

        var output = Render(doc, target, new Add("int y = 2;"));

        output.Should().Contain("int x = 1;\nint y = 2;");
    }

    [Test]
    public void Add_CodeBlock_PreservesLanguageTag()
    {
        var cb = new CodeBlock("python", "x = 1", 1);
        var (doc, target) = DocWith(cb);

        var output = Render(doc, target, new Add("y = 2"));

        output.Should().Contain("```python");
    }

    [Test]
    public void Add_CodeBlock_NullLanguage_RendersEmptyFence()
    {
        var cb = new CodeBlock(null, "code", 1);
        var (doc, target) = DocWith(cb);

        var output = Render(doc, target, new Add("more"));

        // "```" with no language tag -- just verify the fence opens without a language identifier
        output.Should().Contain("```");
        output.Should().NotContain("```csharp");
        output.Should().NotContain("```python");
    }

    // -------------------------------------------------------------------------
    // Add -- BlockQuote
    // -------------------------------------------------------------------------

    [Test]
    public void Add_BlockQuote_AppendsTextWithSpaceSeparator()
    {
        var bq = new BlockQuote("original quote", 1);
        var (doc, target) = DocWith(bq);

        var output = Render(doc, target, new Add("extra"));

        output.Should().Contain("> original quote extra");
    }

    // -------------------------------------------------------------------------
    // Set -- ListItem
    // -------------------------------------------------------------------------

    [Test]
    public void Set_ListItem_ReplacesContent()
    {
        var item = new ListItem("old item", ListKind.Bulleted, 1, null);
        var lb = new ListBlock(ListKind.Bulleted, [item], 1);
        var (doc, _) = DocWith(lb);

        var output = Render(doc, item, new Set("new item"));

        output.Should().Contain("- new item");
        output.Should().NotContain("old item");
    }

    [Test]
    public void Set_ListItem_PreservesSubList()
    {
        var subItem = new ListItem("child", ListKind.Bulleted, 1, null);
        var subList = new ListBlock(ListKind.Bulleted, [subItem], 1);
        var item = new ListItem("parent", ListKind.Bulleted, 1, subList);
        var lb = new ListBlock(ListKind.Bulleted, [item], 1);
        var (doc, _) = DocWith(lb);

        var output = Render(doc, item, new Set("updated parent"));

        output.Should().Contain("updated parent");
        output.Should().Contain("child");
    }

    [Test]
    public void Set_ListItem_PreservesIndex()
    {
        var item1 = new ListItem("first", ListKind.Numbered, 1, null);
        var item2 = new ListItem("second", ListKind.Numbered, 2, null);
        var lb = new ListBlock(ListKind.Numbered, [item1, item2], 1);
        var (doc, _) = DocWith(lb);

        var output = Render(doc, item2, new Set("replaced"));

        output.Should().Contain("2. replaced");
    }

    // -------------------------------------------------------------------------
    // Set -- Section (heading rename)
    // -------------------------------------------------------------------------

    [Test]
    public void Set_Section_ReplacesHeadingText()
    {
        var section = new Section(new Heading("Old Title", 2), [], []);
        var doc = new MarkdownDocument([section]);

        var output = Render(doc, section, new Set("New Title"));

        output.Should().Contain("## New Title");
        output.Should().NotContain("Old Title");
    }

    [Test]
    public void Set_Section_PreservesHeadingLevel()
    {
        var section = new Section(new Heading("Title", 3), [], []);
        var doc = new MarkdownDocument([section]);

        var output = Render(doc, section, new Set("Renamed"));

        // Level 3 heading: exactly "### Renamed", not "# Renamed" or "## Renamed"
        output.Should().Contain("### Renamed");
        output.Should().NotContain("#### Renamed");
    }

    [Test]
    public void Set_Section_PreservesBodyParagraphs()
    {
        var body = new TextBlock("body text", 1);
        var section = new Section(new Heading("Old", 1), [body], []);
        var doc = new MarkdownDocument([section]);

        var output = Render(doc, section, new Set("New"));

        output.Should().Contain("body text");
    }

    // -------------------------------------------------------------------------
    // SyntheticTextBlock source resolution
    // -------------------------------------------------------------------------

    [Test]
    public void Set_SyntheticTextBlock_WrappingListItem_ReplacesListItemContent()
    {
        var item = new ListItem("original", ListKind.Bulleted, 1, null);
        var lb = new ListBlock(ListKind.Bulleted, [item], 1);
        var (doc, _) = DocWith(lb);

        // Simulate what QueryExecutor produces when .text is applied to a ListItem
        var synthetic = new SyntheticTextBlock("original", 1, item);

        var output = Render(doc, synthetic, new Set("replaced via synthetic"));

        output.Should().Contain("- replaced via synthetic");
        output.Should().NotContain("original");
    }

    [Test]
    public void Set_SyntheticTextBlock_WrappingSection_ReplacesHeadingText()
    {
        var section = new Section(new Heading("Old Heading", 1), [], []);
        var doc = new MarkdownDocument([section]);

        var synthetic = new SyntheticTextBlock("Old Heading", 1, section);

        var output = Render(doc, synthetic, new Set("New Heading"));

        output.Should().Contain("# New Heading");
        output.Should().NotContain("Old Heading");
    }

    // -------------------------------------------------------------------------
    // Non-targeted nodes are rendered unchanged
    // -------------------------------------------------------------------------

    [Test]
    public void NonTargetedSiblingTextBlock_IsRenderedUnchanged()
    {
        var sibling = new TextBlock("sibling content", 1);
        var target = new TextBlock("target content", 2);
        var section = new Section(new Heading("S", 1), [sibling, target], []);
        var doc = new MarkdownDocument([section]);

        var output = Render(doc, target, new Set("mutated"));

        output.Should().Contain("sibling content");
        output.Should().Contain("mutated");
        output.Should().NotContain("target content");
    }

    [Test]
    public void NonTargetedSiblingSection_IsRenderedUnchanged()
    {
        var (doc, _, sibling) = DocWithTwoSections();
        var targetSection = doc.Sections[1];

        var output = Render(doc, targetSection, new Set("Renamed Target"));

        output.Should().Contain("# Sibling");
        output.Should().Contain("sibling body");
    }

    [Test]
    public void NonTargetedListItems_AreRenderedUnchanged()
    {
        var item1 = new ListItem("keep me", ListKind.Bulleted, 1, null);
        var item2 = new ListItem("change me", ListKind.Bulleted, 2, null);
        var lb = new ListBlock(ListKind.Bulleted, [item1, item2], 1);
        var (doc, _) = DocWith(lb);

        var output = Render(doc, item2, new Set("changed"));

        output.Should().Contain("- keep me");
        output.Should().Contain("- changed");
        output.Should().NotContain("change me");
    }

    // -------------------------------------------------------------------------
    // Round-trip: render then re-parse produces expected structure
    // -------------------------------------------------------------------------

    [Test]
    public void RoundTrip_Add_TextBlock_ParsedDocumentHasMutatedContent()
    {
        var tb = new TextBlock("Hello", 1);
        var (doc, target) = DocWith(tb);

        var rendered = Render(doc, target, new Add("world"));
        var reparsed = ParseOk(rendered);

        reparsed.Sections.Should().HaveCount(1);
        var para = reparsed.Sections[0].Paragraphs[0].Should().BeOfType<TextBlock>().Subject;
        para.Content.Should().Be("Hello world");
    }

    [Test]
    public void RoundTrip_Add_ListBlock_ParsedDocumentHasNewItem()
    {
        var lb = new ListBlock(ListKind.Bulleted,
            [new ListItem("a", ListKind.Bulleted, 1, null)], 1);
        var (doc, target) = DocWith(lb);

        var rendered = Render(doc, target, new Add("b"));
        var reparsed = ParseOk(rendered);

        var list = reparsed.Sections[0].Paragraphs[0].Should().BeOfType<ListBlock>().Subject;
        list.Items.Should().HaveCount(2);
        list.Items[1].Content.Should().Be("b");
    }

    [Test]
    public void RoundTrip_Set_Section_ParsedDocumentHasNewHeadingText()
    {
        var section = new Section(new Heading("Original", 2), [], []);
        var doc = new MarkdownDocument([section]);

        var rendered = Render(doc, section, new Set("Updated"));
        var reparsed = ParseOk(rendered);

        reparsed.Sections[0].Heading.Text.Should().Be("Updated");
        reparsed.Sections[0].Heading.Level.Should().Be(2);
    }

    [Test]
    public void RoundTrip_Set_ListItem_ParsedDocumentHasReplacedContent()
    {
        var item = new ListItem("before", ListKind.Bulleted, 1, null);
        var lb = new ListBlock(ListKind.Bulleted, [item], 1);
        var (doc, _) = DocWith(lb);

        var rendered = Render(doc, item, new Set("after"));
        var reparsed = ParseOk(rendered);

        var list = reparsed.Sections[0].Paragraphs[0].Should().BeOfType<ListBlock>().Subject;
        list.Items[0].Content.Should().Be("after");
    }

    [Test]
    public void RoundTrip_Add_CodeBlock_ParsedDocumentHasAppendedLine()
    {
        var cb = new CodeBlock("cs", "line1", 1);
        var (doc, target) = DocWith(cb);

        var rendered = Render(doc, target, new Add("line2"));
        var reparsed = ParseOk(rendered);

        var code = reparsed.Sections[0].Paragraphs[0].Should().BeOfType<CodeBlock>().Subject;
        // AppendLine uses Environment.NewLine; the parser trims and normalises line endings
        code.Content.Should().ContainAll("line1", "line2");
        code.Content.Should().Contain("line1");
        code.Content.Should().Contain("line2");
        // Verify line1 comes before line2
        code.Content.IndexOf("line1").Should().BeLessThan(code.Content.IndexOf("line2"));
    }
}
