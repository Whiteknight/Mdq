using Mdq.Core.DocumentModel;
using Mdq.Core.Shared;

namespace Mdq.Core.Editing;

public static class EditValidator
{
    public static Result<IReadOnlyList<MatchableItem>, EditError> Validate(
        IReadOnlyList<MatchableItem> targets,
        EditOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.Text))
            return new EmptyText();

        if (targets.Count == 0)
            return new NoMatchingNode();

        if (operation is Set && targets.Count > 1)
            return new MultipleMatchingNodes(targets.Count);

        return ValidateNodeTypes(targets, operation);
    }

    private static Result<IReadOnlyList<MatchableItem>, EditError> ValidateNodeTypes(
        IReadOnlyList<MatchableItem> targets,
        EditOperation operation)
    {
        foreach (var target in targets)
        {
            var resolved = ResolveSource(target);
            var error = CheckNodeTypeSupport(resolved, operation);
            if (error is not null)
                return error;
        }

        return new Result<IReadOnlyList<MatchableItem>, EditError>.Ok(targets);
    }

    private static EditError? CheckNodeTypeSupport(MatchableItem resolved, EditOperation operation)
        => operation switch
        {
            Add => IsAddSupported(resolved) ? null : UnsupportedFor(resolved, "add"),
            Set => IsSetSupported(resolved) ? null : UnsupportedFor(resolved, "set"),
            _ => null
        };

    private static bool IsAddSupported(MatchableItem resolved)
        => resolved is TextBlock or ListBlock or CodeBlock or BlockQuote;

    private static bool IsSetSupported(MatchableItem resolved)
        => resolved is TextBlock or ListItem or Section;

    private static UnsupportedNodeType UnsupportedFor(MatchableItem resolved, string operation)
        => new(NodeTypeName(resolved), operation);

    private static MatchableItem ResolveSource(MatchableItem target)
        => target is SyntheticTextBlock synthetic ? synthetic.Source : target;

    private static string NodeTypeName(MatchableItem item)
        => item.GetType().Name;
}
