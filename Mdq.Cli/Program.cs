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
        if (args.Length != 2)
        {
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
            return 1;
        }

        if (args[0] == "--toc")
            return ExecuteSelectorAndFile(".flatten[type=heading]", args[1]);

        return ExecuteSelectorAndFile(args[0], args[1]);
    }

    private static int ExecuteSelectorAndFile(string selector, string filePath)
    {
        return ReadFile(filePath)
            .Bind(MarkdownParser.Parse)
            .With(_ => SelectorParser.Parse(selector))
            .Bind((args) => QueryExecutor.Execute(args.Item1, args.Item2))
            .Map(Renderer.Render)
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
