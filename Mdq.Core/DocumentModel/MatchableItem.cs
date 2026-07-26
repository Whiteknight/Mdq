using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;

namespace Mdq.Core.DocumentModel;

public abstract record MatchableItem
{
    [DebuggerHidden]
    public StringSegment LeadingTrivia { get; init; }
    [DebuggerHidden]
    public StringSegment TrailingTrivia { get; init; }

    public abstract bool IsMatch(string property, string op, string value);
}

public record MarkdownDocument(Section TopLevelSection) : MatchableItem
{
    public static MarkdownDocument Empty(StringSegment buffer)
        => new MarkdownDocument(new Section(Heading.Empty, [], []))
        {
            LeadingTrivia = buffer
        };

    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "document") => true,
            _ => false
        };
    }
}

public record Heading(StringSegment Text, int Level) : MatchableItem
{
    public static Heading Empty => new Heading(StringSegment.Empty, 0);

    public bool IsMatch(string sectionHeading)
    {
        if (Level == 0 && !Text.HasValue)
            return false;

        if (string.IsNullOrEmpty(sectionHeading))
            return true;

        var text = Text.HasValue ? Text.Value! : string.Empty;
        var regexString = "^" + Regex.Escape(sectionHeading).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

        return new Regex(regexString, RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .IsMatch(text);
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

public record TextBlock(StringSegment Content, int Index) : Paragraph(Index)
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

public sealed record SyntheticTextBlock(StringSegment Content, int Index, MatchableItem Source) : TextBlock(Content, Index)
{
    public override bool IsMatch(string property, string op, string value) => base.IsMatch(property, op, value);
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

public sealed record BlockQuote(StringSegment Content, int Index) : Paragraph(Index)
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

public sealed record CodeBlock(StringSegment Language, StringSegment Content, int Index) : Paragraph(Index)
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "codeblock") => true,
            ("lang", "=", _) => string.IsNullOrEmpty(value) || value.Equals(Language, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}

public sealed record TableRow(IReadOnlyList<TableCell> Cells, int Index) : MatchableItem
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "tablerow") => true,
            _ => false
        };
    }
}

public sealed record TableBlock(TableRow Header, IReadOnlyList<TableRow> Rows, int Index) : Paragraph(Index)
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "table") => true,
            _ => false
        };
    }
}

public sealed record TableCell(StringSegment Content, int Index) : MatchableItem
{
    public override bool IsMatch(string property, string op, string value)
    {
        return (property, op, value) switch
        {
            ("type", "=", "tablecell") => true,
            _ => false
        };
    }
}

public record ListItem(
    StringSegment Content,
    ListKind Kind,
    int Index,
    ListBlock? SubList = null) : MatchableItem
{
    public override bool IsMatch(string property, string op, string value)
    {
        var content = Content.HasValue ? Content.Value! : string.Empty;
        return (property, op, value) switch
        {
            ("type", "=", "listitem") => true,
            ("checkable", "=", "true") => content.StartsWith("[ ]") || content.StartsWith("[x]"),
            ("checkable", "=", "false") => !content.StartsWith("[ ]") && !content.StartsWith("[x]"),
            ("checked", "=", "true") => content.StartsWith("[x]"),
            ("checked", "=", "false") => !content.StartsWith("[x]"),
            ("optional", "=", "true") => content.StartsWith("[ ]*") || content.StartsWith("[x]*"),
            ("optional", "=", "false") => !content.StartsWith("[ ]*") && !content.StartsWith("[x]*"),
            _ => false
        };
    }
}
