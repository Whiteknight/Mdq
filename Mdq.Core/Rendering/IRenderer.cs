using Mdq.Core.DocumentModel;

namespace Mdq.Core.Rendering;

public interface IRenderer
{
    string Render(List<MatchableItem> items);
}
