using Mdq.Core.Shared;

namespace Mdq.Core.QueryEngine;

public abstract record QueryError(string Message) : MdqError(Message)
{
    /// <summary>No section with the given heading name exists at the expected level.</summary>
    public sealed record HeadingNotFound(string Name, int Level)
        : QueryError($"Heading '{Name}' not found at level {Level}");

    /// <summary>The requested paragraph index exceeds the number of paragraphs in the section.</summary>
    public sealed record ParagraphOutOfRange(int Requested, int Actual)
        : QueryError($"Paragraph {Requested} requested but section has {Actual} paragraphs");

    /// <summary>The requested item index exceeds the number of items in the list.</summary>
    public sealed record ItemOutOfRange(int Requested, int Actual)
        : QueryError($"Item {Requested} requested but list has {Actual} items");

    /// <summary>An .item(N) selector was applied to a paragraph that is not a list.</summary>
    public sealed record NotAList()
        : QueryError("The selected paragraph is not a list");
}
