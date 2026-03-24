namespace Mdq.Core.SelectorModel;

public abstract record Selector
{
    public static Selector PoundHeading(string name) => new Heading(name);
    public static Selector DotText() => new Text();
    public static Selector DotHeading() => new HeadingContent();
    public static Selector DotParagraphParenIndex(int index) => new ParagraphAt(index);
    public static Selector DotItemParenIndex(int index) => new ItemAt(index);
    public static Selector DotItems() => new Items();
    public static Selector DotFlatten() => new Flatten();
    public static Selector ErrorMessage(string message) => new Error(message);
    public static Selector FilterBlock(string property, string op, string value) => new Filter(property, op, value);

    public sealed record Error(string Message) : Selector;

    /// <summary>#Name -- navigate into a section by heading text.</summary>
    public sealed record Heading(string Name) : Selector
    {
        public override string ToString() => $"#{Name}";
    }

    /// <summary>.text -- body content of the current section, excluding the heading line.</summary>
    public sealed record Text : Selector
    {
        public override string ToString() => ".text";
    }

    /// <summary>.heading -- heading text of the current section, without # prefix characters.</summary>
    public sealed record HeadingContent : Selector
    {
        public override string ToString() => ".heading";
    }

    /// <summary>.paragraph(N) -- Nth paragraph (1-indexed) within the current section.</summary>
    public sealed record ParagraphAt(int Index) : Selector
    {
        public override string ToString() => $".paragraph({Index})";
    }

    /// <summary>.item(N) -- Nth list item (1-indexed) within the current list or sub-list.</summary>
    public sealed record ItemAt(int Index) : Selector
    {
        public override string ToString() => $".item({Index})";
    }

    public sealed record Filter(string Property, string Operator, string Value) : Selector
    {
        public override string ToString() => $"[{Property}{Operator}{Value}]";
    }

    public sealed record Items() : Selector
    {
        public override string ToString() => ".items";
    }

    public sealed record Flatten() : Selector
    {
        public override string ToString() => ".flatten";
    }
}
