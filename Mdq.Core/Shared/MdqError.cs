namespace Mdq.Core.Shared;

public abstract record MdqError(string Message);

public record UnknownMdqError(string Message) : MdqError(Message);
