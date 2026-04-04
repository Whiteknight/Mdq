namespace Mdq.Cli.Arguments;

public abstract record Mode();

public sealed record HelpMode(string? ErrorMessage = null) : Mode;

public abstract record FileMode(string FilePath) : Mode;

public sealed record TocMode(string FilePath) : FileMode(FilePath);

public sealed record QueryMode(string Selector, string FilePath) : FileMode(FilePath);
