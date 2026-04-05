namespace Mdq.Core.Editing;

public abstract record EditOperation(string Text);

public sealed record Add(string Text) : EditOperation(Text);

public sealed record Set(string Text) : EditOperation(Text);
