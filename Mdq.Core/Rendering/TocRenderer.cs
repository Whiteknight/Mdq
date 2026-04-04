using System.Text;
using Mdq.Core.DocumentModel;

namespace Mdq.Core.Rendering;

public class TocRenderer : IRenderer
{
    private readonly record struct Node(Heading Heading, List<Node> Children);

    public string Render(List<MatchableItem> items)
    {
        var headings = items.OfType<Heading>().ToList();
        headings.Insert(0, new Heading("", 0));
        var root = BuildNode(headings, 0, headings[0].Level).Node;

        var sb = new StringBuilder();
        Render(sb, root.Children.Count == 1 ? root.Children[0] : root, "", "");
        return sb.ToString();
    }

    private static (Node Node, int Index) BuildNode(List<Heading> headings, int index, int level)
    {
        var current = headings[index++];

        var children = new List<Node>();
        for (; index < headings.Count; index++)
        {
            var next = headings[index];
            if (next.Level <= level)
                return (new Node(current, children), index - 1);

            (var child, index) = BuildNode(headings, index, next.Level);
            children.Add(child);
        }

        return (new Node(current, children), index);
    }

    private static void Render(StringBuilder sb, Node node, string prefix, string connector)
    {
        if (node.Heading.Level != 0)
            sb.AppendLine(prefix + connector + node.Heading.Text);
        for (int i = 0; i < node.Children.Count; i++)
        {
            var child = node.Children[i];
            var isLast = i == node.Children.Count - 1;
            Render(sb, child, prefix + (connector == "├─ " ? "│  " : "   "), isLast ? "└─ " : "├─ ");
        }
    }
}
