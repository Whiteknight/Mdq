using Mdq.Core.Shared;

namespace Mdq.Core.SelectorModel;

public record SelectorParseError(string Message, int Position) : MdqError(Message);
