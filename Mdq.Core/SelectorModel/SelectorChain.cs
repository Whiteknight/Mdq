using System.Text;

namespace Mdq.Core.SelectorModel;

public record SelectorChain(IReadOnlyList<SelectorSegment> Segments)
{
    public bool IsEmpty => Segments.Count == 0;

    public override string ToString()
    {
        var sb = new StringBuilder();
        foreach (var segment in Segments)
            sb.Append(segment.ToString());
        return sb.ToString();
    }
}
