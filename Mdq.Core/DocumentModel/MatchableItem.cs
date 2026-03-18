namespace Mdq.Core.DocumentModel;

public abstract record MatchableItem;

public record Section(
    string? HeadingText,
    int HeadingLevel,
    IReadOnlyList<Paragraph> Paragraphs,
    IReadOnlyList<Section> Children) : MatchableItem;

public enum ListKind
{
    Bulleted,
    Numbered
}

public abstract record Paragraph : MatchableItem;

public sealed record TextBlock(string Content) : Paragraph;

public sealed record ListBlock(ListKind Kind, IReadOnlyList<ListItem> Items) : Paragraph;

public sealed record BlockQuote(string Content) : Paragraph;

public record ListItem(
    string Content,
    ListBlock? SubList) : MatchableItem;
