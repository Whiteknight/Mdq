using System.Text.RegularExpressions;

namespace Mdq.Core.DocumentModel;

public abstract record MatchableItem
{
    public abstract bool IsMatch(string property, string op, string value);
}

public record MarkdownDocument(IReadOnlyList<Section> Sections) : MatchableItem
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "document") => true,
            _ => false
        };
    }
}

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

    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "heading") => true,
            ("level", "=", _) => int.TryParse(value, out var parsed) && parsed == Level,
            _ => false
        };
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

    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "section") => true,
            ("level", "=", _) => int.TryParse(value, out var parsed) && parsed == Heading.Level,
            _ => false
        };
    }
}

public enum ListKind
{
    Bulleted,
    Numbered
}

public abstract record Paragraph(int Index) : MatchableItem;

public sealed record TextBlock(string Content, int Index) : Paragraph(Index)
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "text") => true,
            _ => false
        };
    }
}

public sealed record ListBlock(ListKind Kind, IReadOnlyList<ListItem> Items, int Index) : Paragraph(Index)
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "list") => true,
            ("kind", "=", "bullet") => Kind == ListKind.Bulleted,
            ("kind", "=", "numbered") => Kind == ListKind.Numbered,
            _ => false
        };
    }
}

public sealed record BlockQuote(string Content, int Index) : Paragraph(Index)
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "blockquote") => true,
            _ => false
        };
    }
}

public sealed record CodeBlock(string? Language, string Content, int Index) : Paragraph(Index)
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "codeblock") => true,
            ("lang", "=", _) => (string.IsNullOrEmpty(Language) && string.IsNullOrEmpty(value)) || value.Equals(Language, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}

public record ListItem(
    string Content,
    ListKind Kind,
    int Index,
    ListBlock? SubList) : MatchableItem
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "listitem") => true,
            ("checkable", "=", "true") => Content.StartsWith("[ ]") || Content.StartsWith("[x]"),
            ("checkable", "=", "false") => !Content.StartsWith("[ ]") && !Content.StartsWith("[x]"),
            ("checked", "=", "true") => Content.StartsWith("[x]"),
            ("checked", "=", "false") => !Content.StartsWith("[x]"),
            ("optional", "=", "true") => Content.StartsWith("[ ]*") || Content.StartsWith("[x]*"),
            ("optional", "=", "false") => !Content.StartsWith("[ ]*") && !Content.StartsWith("[x]*"),
            _ => false
        };
    }
}
