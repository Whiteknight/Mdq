using Mdq.Core.Shared;

namespace Mdq.Core.DocumentModel;

public record MarkdownParseError(string Message) : MdqError(Message);
