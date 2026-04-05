using Mdq.Core.Shared;

namespace Mdq.Core.Editing;

public abstract record EditError(string Message) : MdqError(Message);

public sealed record EmptyText()
    : EditError("Text argument must not be empty");

public sealed record NoMatchingNode()
    : EditError("Selector resolved to zero nodes; nothing to edit");

public sealed record MultipleMatchingNodes(int Count)
    : EditError($"--set resolved to {Count} nodes; exactly one node is required");

public sealed record UnsupportedNodeType(string NodeType, string Operation)
    : EditError($"Node type '{NodeType}' does not support the '{Operation}' operation");
