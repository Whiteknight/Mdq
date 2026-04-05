using AwesomeAssertions;
using Mdq.Core.Editing;

namespace Mdq.Tests.Editing;

[TestFixture]
public class EditErrorTests
{
    [Test]
    public void EmptyText_Message_IsCorrect()
    {
        var error = new EmptyText();
        error.Message.Should().Be("Text argument must not be empty");
    }

    [Test]
    public void NoMatchingNode_Message_IsCorrect()
    {
        var error = new NoMatchingNode();
        error.Message.Should().Be("Selector resolved to zero nodes; nothing to edit");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(10)]
    public void MultipleMatchingNodes_Message_IncludesCount(int count)
    {
        var error = new MultipleMatchingNodes(count);
        error.Message.Should().Be($"--set resolved to {count} nodes; exactly one node is required");
    }

    [TestCase("TextBlock", "add")]
    [TestCase("Section", "add")]
    [TestCase("ListBlock", "set")]
    [TestCase("CodeBlock", "set")]
    public void UnsupportedNodeType_Message_IncludesNodeTypeAndOperation(string nodeType, string operation)
    {
        var error = new UnsupportedNodeType(nodeType, operation);
        error.Message.Should().Be($"Node type '{nodeType}' does not support the '{operation}' operation");
    }

    [Test]
    public void AllEditErrors_AreSubtypesOfMdqError()
    {
        new EmptyText().Should().BeAssignableTo<Mdq.Core.Shared.MdqError>();
        new NoMatchingNode().Should().BeAssignableTo<Mdq.Core.Shared.MdqError>();
        new MultipleMatchingNodes(2).Should().BeAssignableTo<Mdq.Core.Shared.MdqError>();
        new UnsupportedNodeType("X", "y").Should().BeAssignableTo<Mdq.Core.Shared.MdqError>();
    }
}
