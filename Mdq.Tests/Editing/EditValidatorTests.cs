using AwesomeAssertions;
using Mdq.Core.DocumentModel;
using Mdq.Core.Editing;
using Mdq.Core.Shared;

namespace Mdq.Tests.Editing;

[TestFixture]
public class EditValidatorTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TextBlock ATextBlock(string content = "some content") =>
        new(content, 1);

    private static ListBlock AListBlock() =>
        new(ListKind.Bulleted, [new ListItem("item", ListKind.Bulleted, 1, null)], 1);

    private static CodeBlock ACodeBlock() =>
        new(null, "code", 1);

    private static BlockQuote ABlockQuote() =>
        new("quote", 1);

    private static ListItem AListItem() =>
        new("item content", ListKind.Bulleted, 1, null);

    private static Section ASection() =>
        new(new Heading("Title", 1), [], []);

    private static Result<IReadOnlyList<MatchableItem>, EditError> Validate(
        IReadOnlyList<MatchableItem> targets,
        EditOperation operation) =>
        EditValidator.Validate(targets, operation);

    // -------------------------------------------------------------------------
    // EmptyText
    // -------------------------------------------------------------------------

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t")]
    public void EmptyText_ReturnedFor_EmptyOrWhitespaceText_WithAdd(string text)
    {
        var result = Validate([ATextBlock()], new Add(text));
        result.IsError.Should().BeTrue();
        result.GetErrorOrDefault().Should().BeOfType<EmptyText>();
    }

    [TestCase("")]
    [TestCase("   ")]
    public void EmptyText_ReturnedFor_EmptyOrWhitespaceText_WithSet(string text)
    {
        var result = Validate([ATextBlock()], new Set(text));
        result.IsError.Should().BeTrue();
        result.GetErrorOrDefault().Should().BeOfType<EmptyText>();
    }

    [Test]
    public void EmptyText_TakesPrecedenceOver_NoMatchingNode()
    {
        var result = Validate([], new Add(""));
        result.GetErrorOrDefault().Should().BeOfType<EmptyText>();
    }

    // -------------------------------------------------------------------------
    // NoMatchingNode
    // -------------------------------------------------------------------------

    [Test]
    public void NoMatchingNode_ReturnedFor_EmptyTargetList_WithAdd()
    {
        var result = Validate([], new Add("text"));
        result.IsError.Should().BeTrue();
        result.GetErrorOrDefault().Should().BeOfType<NoMatchingNode>();
    }

    [Test]
    public void NoMatchingNode_ReturnedFor_EmptyTargetList_WithSet()
    {
        var result = Validate([], new Set("text"));
        result.IsError.Should().BeTrue();
        result.GetErrorOrDefault().Should().BeOfType<NoMatchingNode>();
    }

    // -------------------------------------------------------------------------
    // MultipleMatchingNodes
    // -------------------------------------------------------------------------

    [Test]
    public void MultipleMatchingNodes_ReturnedFor_Set_WithMoreThanOneTarget()
    {
        var targets = new MatchableItem[] { ATextBlock(), ATextBlock() };
        var result = Validate(targets, new Set("text"));
        result.IsError.Should().BeTrue();
        var error = result.GetErrorOrDefault().Should().BeOfType<MultipleMatchingNodes>().Subject;
        error.Count.Should().Be(2);
    }

    [Test]
    public void MultipleMatchingNodes_Count_ReflectsActualTargetCount()
    {
        var targets = new MatchableItem[] { ATextBlock(), ATextBlock(), ATextBlock() };
        var result = Validate(targets, new Set("text"));
        var error = result.GetErrorOrDefault().Should().BeOfType<MultipleMatchingNodes>().Subject;
        error.Count.Should().Be(3);
    }

    [Test]
    public void Add_WithMultipleTargets_PassesThrough()
    {
        var targets = new MatchableItem[] { ATextBlock(), ATextBlock() };
        var result = Validate(targets, new Add("text"));
        result.IsSuccess.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // UnsupportedNodeType -- Add
    // -------------------------------------------------------------------------

    [Test]
    public void Add_OnSection_ReturnsUnsupportedNodeType()
    {
        var result = Validate([ASection()], new Add("text"));
        var error = result.GetErrorOrDefault().Should().BeOfType<UnsupportedNodeType>().Subject;
        error.NodeType.Should().Be(nameof(Section));
        error.Operation.Should().Be("add");
    }

    [Test]
    public void Add_OnListItem_ReturnsUnsupportedNodeType()
    {
        var result = Validate([AListItem()], new Add("text"));
        var error = result.GetErrorOrDefault().Should().BeOfType<UnsupportedNodeType>().Subject;
        error.NodeType.Should().Be(nameof(ListItem));
        error.Operation.Should().Be("add");
    }

    // -------------------------------------------------------------------------
    // UnsupportedNodeType -- Set
    // -------------------------------------------------------------------------

    [Test]
    public void Set_OnListBlock_ReturnsUnsupportedNodeType()
    {
        var result = Validate([AListBlock()], new Set("text"));
        var error = result.GetErrorOrDefault().Should().BeOfType<UnsupportedNodeType>().Subject;
        error.NodeType.Should().Be(nameof(ListBlock));
        error.Operation.Should().Be("set");
    }

    [Test]
    public void Set_OnCodeBlock_ReturnsUnsupportedNodeType()
    {
        var result = Validate([ACodeBlock()], new Set("text"));
        var error = result.GetErrorOrDefault().Should().BeOfType<UnsupportedNodeType>().Subject;
        error.NodeType.Should().Be(nameof(CodeBlock));
        error.Operation.Should().Be("set");
    }

    [Test]
    public void Set_OnBlockQuote_ReturnsUnsupportedNodeType()
    {
        var result = Validate([ABlockQuote()], new Set("text"));
        var error = result.GetErrorOrDefault().Should().BeOfType<UnsupportedNodeType>().Subject;
        error.NodeType.Should().Be(nameof(BlockQuote));
        error.Operation.Should().Be("set");
    }

    // -------------------------------------------------------------------------
    // Supported combinations -- Add
    // -------------------------------------------------------------------------

    [Test]
    public void Add_OnTextBlock_ReturnsOk() =>
        Validate([ATextBlock()], new Add("text")).IsSuccess.Should().BeTrue();

    [Test]
    public void Add_OnListBlock_ReturnsOk() =>
        Validate([AListBlock()], new Add("text")).IsSuccess.Should().BeTrue();

    [Test]
    public void Add_OnCodeBlock_ReturnsOk() =>
        Validate([ACodeBlock()], new Add("text")).IsSuccess.Should().BeTrue();

    [Test]
    public void Add_OnBlockQuote_ReturnsOk() =>
        Validate([ABlockQuote()], new Add("text")).IsSuccess.Should().BeTrue();

    // -------------------------------------------------------------------------
    // Supported combinations -- Set
    // -------------------------------------------------------------------------

    [Test]
    public void Set_OnTextBlock_ReturnsOk() =>
        Validate([ATextBlock()], new Set("text")).IsSuccess.Should().BeTrue();

    [Test]
    public void Set_OnListItem_ReturnsOk() =>
        Validate([AListItem()], new Set("text")).IsSuccess.Should().BeTrue();

    [Test]
    public void Set_OnSection_ReturnsOk() =>
        Validate([ASection()], new Set("text")).IsSuccess.Should().BeTrue();

    // -------------------------------------------------------------------------
    // SyntheticTextBlock source resolution
    // -------------------------------------------------------------------------

    [Test]
    public void Add_OnSyntheticTextBlock_WrappingListItem_ReturnsUnsupportedNodeType()
    {
        // SyntheticTextBlock.Source is a ListItem; Add does not support ListItem
        var source = AListItem();
        var synthetic = new SyntheticTextBlock("item content", 1, source);

        var result = Validate([synthetic], new Add("text"));

        var error = result.GetErrorOrDefault().Should().BeOfType<UnsupportedNodeType>().Subject;
        error.NodeType.Should().Be(nameof(ListItem));
        error.Operation.Should().Be("add");
    }

    [Test]
    public void Set_OnSyntheticTextBlock_WrappingListItem_ReturnsOk()
    {
        // SyntheticTextBlock.Source is a ListItem; Set supports ListItem
        var source = AListItem();
        var synthetic = new SyntheticTextBlock("item content", 1, source);

        var result = Validate([synthetic], new Set("text"));

        result.IsSuccess.Should().BeTrue();
    }

    [Test]
    public void Add_OnSyntheticTextBlock_WrappingSection_ReturnsUnsupportedNodeType()
    {
        var source = ASection();
        var synthetic = new SyntheticTextBlock("heading text", 1, source);

        var result = Validate([synthetic], new Add("text"));

        var error = result.GetErrorOrDefault().Should().BeOfType<UnsupportedNodeType>().Subject;
        error.NodeType.Should().Be(nameof(Section));
    }

    // -------------------------------------------------------------------------
    // Ok result returns the original targets list
    // -------------------------------------------------------------------------

    [Test]
    public void Ok_Result_ContainsOriginalTargets()
    {
        var targets = new MatchableItem[] { ATextBlock("hello") };
        var result = Validate(targets, new Add("world"));
        result.GetValueOrDefault().Should().BeSameAs(targets);
    }
}
