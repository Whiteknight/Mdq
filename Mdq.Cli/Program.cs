using Mdq.Cli.Arguments;
using Mdq.Core.DocumentModel;
using Mdq.Core.QueryEngine;
using Mdq.Core.Rendering;
using Mdq.Core.SelectorModel;
using Mdq.Core.Shared;

namespace Mdq.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        var mode = ArgumentParser.Parse(args);
        switch (mode)
        {
            case HelpMode hm:
                return PrintHelp(hm);

            case TocMode tocm:
                return PrintTableOfContents(tocm);

            case QueryMode qm:
                return ExtractQuery(qm);

            default:
                return PrintHelp(new HelpMode { ErrorMessage = "Unknown mode. Use --help for usage instructions." });
        }
    }

    private static int PrintHelp(HelpMode help)
    {
        if (!string.IsNullOrEmpty(help.ErrorMessage))
        {
            Console.Error.WriteLine($"Error: {help.ErrorMessage}");
            Console.Error.WriteLine();
        }
        Console.Error.WriteLine("Usage: mdq <selector> <file>");
        Console.Error.WriteLine("       mdq --toc <file>");
        Console.Error.WriteLine();
        Console.Error.WriteLine("  <selector>  Query selector string (e.g. \"#Introduction.text\")");
        Console.Error.WriteLine("  <file>      Path to the Markdown file to query");
        Console.Error.WriteLine("  --toc       Only print headings, like a table of contents");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  mdq \"\" README.md");
        Console.Error.WriteLine("  mdq \"#Installation\" README.md ");
        Console.Error.WriteLine("  mdq \"#Usage.paragraph(1)\" README.md");

        return string.IsNullOrEmpty(help.ErrorMessage) ? 0 : 1;
    }

    private static int PrintTableOfContents(TocMode toc)
    {
        return ExecuteSelectorAndFile(new TocRenderer(), ".flatten[type=heading]", toc.FilePath);
    }

    private static int ExtractQuery(QueryMode query)
    {
        return ExecuteSelectorAndFile(new MarkdownRenderer(), query.Selector, query.FilePath);
    }

    private static int ExecuteSelectorAndFile(IRenderer renderer, string selector, string filePath)
    {
        return ReadFile(filePath)
            .Bind(MarkdownParser.Parse)
            .With(_ => SelectorParser.Parse(selector))
            .Bind((args) => QueryExecutor.Execute(args.Item1, args.Item2))
            .Map(renderer.Render)
            .Switch(
                s => Console.WriteLine(s),
                e => Console.Error.WriteLine($"Error: {e.Message}"))
            .Match(
                _ => 0,
                _ => 1);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Result<string, MdqError> ReadFile(string path)
    {
        if (!File.Exists(path))
            return new UnknownMdqError($"File not found: {path}");

        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            return new UnknownMdqError($"Could not read file '{path}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new UnknownMdqError($"Access denied reading file '{path}': {ex.Message}");
        }
    }
}
