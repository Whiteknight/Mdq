using Mdq.Core.Editing;

namespace Mdq.Cli.Arguments;

public static class ArgumentParser
{
    public static Mode Parse(string[] args)
    {
        if (args.Length == 0 || args.Any(a => a == "--help" || a == "-h"))
            return new HelpMode();

        if (args.Length == 2 && args[0] == "--toc")
            return new TocMode(args[1]);

        if (args.Length == 3 && args[0] == "--query")
            return new QueryMode(args[1], args[2]);

        if (IsEditVerb(args[0]) && args.Length >= 2 && args[1] == "--in-place")
            return ParseEditMode(args, inPlace: true);

        if (IsEditVerb(args[0]))
            return ParseEditMode(args, inPlace: false);

        if (args.Length == 2)
            return new QueryMode(args[0], args[1]);

        return new HelpMode("Unknown arguments");
    }

    private static bool IsEditVerb(string arg) => arg == "--add" || arg == "--set";

    private static Mode ParseEditMode(string[] args, bool inPlace)
    {
        var verb = args[0];
        var rest = inPlace ? args[2..] : args[1..];

        if (rest.Length < 1)
            return new HelpMode($"'{verb}' requires a <selector> argument.");

        if (rest.Length < 2)
            return new HelpMode($"'{verb}' requires a <file> argument.");

        if (rest.Length < 3)
            return new HelpMode($"'{verb}' requires a <text> argument.");

        var selector = rest[0];
        var filePath = rest[1];
        var text = rest[2];
        EditOperation operation = verb == "--add" ? new Add(text) : new Set(text);

        return new EditMode(operation, selector, filePath, inPlace);
    }
}
