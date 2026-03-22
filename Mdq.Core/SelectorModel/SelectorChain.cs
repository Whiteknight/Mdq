using System.Text;

namespace Mdq.Core.SelectorModel;

public readonly record struct SelectorChain(IReadOnlyList<Selector> Segments)
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
