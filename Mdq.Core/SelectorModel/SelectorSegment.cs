namespace Mdq.Core.SelectorModel;

public abstract record SelectorSegment
{
    /// <summary>#Name -- navigate into a section by heading text.</summary>
    public sealed record Heading(string Name) : SelectorSegment
    {
        public override string ToString() => $"#{Name}";
    }

    /// <summary>.text -- body content of the current section, excluding the heading line.</summary>
    public sealed record Text : SelectorSegment
    {
        public override string ToString() => ".text";
    }

    /// <summary>.heading -- heading text of the current section, without # prefix characters.</summary>
    public sealed record HeadingContent : SelectorSegment
    {
        public override string ToString() => ".heading";
    }

    /// <summary>.paragraph(N) -- Nth paragraph (1-indexed) within the current section.</summary>
    public sealed record ParagraphAt(int Index) : SelectorSegment
    {
        public override string ToString() => $".paragraph({Index})";
    }

    /// <summary>.item(N) -- Nth list item (1-indexed) within the current list or sub-list.</summary>
    public sealed record ItemAt(int Index) : SelectorSegment
    {
        public override string ToString() => $".item({Index})";
    }
}
