using System.Text.RegularExpressions;

namespace Mdq.Core.DocumentModel;

public abstract record MatchableItem;

// TODO: A document can contain paragraphs ahead of any section.
public record MarkdownDocument(IReadOnlyList<Section> Sections) : MatchableItem;

public record Heading(string? Text, int Level) : MatchableItem
{
    public static Heading Empty => new(null, 0);

    public bool IsMatch(string sectionHeading)
    {
        // Top-level contents, before the first heading, should not be returned if we do #.
        // # should always step us at least one level, so the first # should take us to 1, etc.
        if (Level == 0 && Text == null)
            return false;

        if (string.IsNullOrEmpty(sectionHeading))
            return true;

        var regexString = "^" + Regex.Escape(sectionHeading ?? string.Empty).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

        return new Regex(regexString, RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .IsMatch(Text ?? string.Empty);
    }
}

public record Section(
   Heading Heading,
   IReadOnlyList<Paragraph> Paragraphs,
   IReadOnlyList<Section> Children) : MatchableItem
{
    public string ToBodyString()
    {
        var parts = new List<string>();

        foreach (var para in Paragraphs)
            parts.Add(para.ToString());

        foreach (var child in Children)
            parts.Add(child.ToString());

        return string.Join("\n\n", parts);
    }
}

public enum ListKind
{
    Bulleted,
    Numbered
}

public abstract record Paragraph(int Index) : MatchableItem;

public sealed record TextBlock(string Content, int Index) : Paragraph(Index);

public sealed record ListBlock(ListKind Kind, IReadOnlyList<ListItem> Items, int Index) : Paragraph(Index);

public sealed record BlockQuote(string Content, int Index) : Paragraph(Index);

public record ListItem(
    string Content,
    ListKind Kind,
    int Index,
    ListBlock? SubList) : MatchableItem;
