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

        if (args.Length == 2)
            return new QueryMode(args[0], args[1]);

        return new HelpMode("Unknown arguments");
    }
}
