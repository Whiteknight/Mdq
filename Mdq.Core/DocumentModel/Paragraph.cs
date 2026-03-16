namespace Mdq.Core.DocumentModel;

public enum ListKind { Bulleted, Numbered }

public abstract record Paragraph
{
    public sealed record TextBlock(string Content) : Paragraph;
    public sealed record ListBlock(ListKind Kind, IReadOnlyList<ListItem> Items) : Paragraph;
    public sealed record BlockQuote(string Content) : Paragraph;
}
