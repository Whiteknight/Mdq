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
            Console.Error.WriteLine("Usage: mdq <file> <selector>");
            Console.Error.WriteLine();
            Console.Error.WriteLine("  <file>      Path to the Markdown file to query");
            Console.Error.WriteLine("  <selector>  Query selector string (e.g. \"#Introduction.text\")");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Examples:");
            Console.Error.WriteLine("  mdq README.md \"\"");
            Console.Error.WriteLine("  mdq README.md \"#Installation\"");
            Console.Error.WriteLine("  mdq README.md \"#Usage.paragraph(1)\"");
            return 1;
        }

        var filePath = args[0];
        var selectorArg = args[1];

        return ReadFile(filePath)
            .Bind(MarkdownParser.Parse)
            .With(_ => SelectorParser.Parse(selectorArg))
            .Bind((args) => QueryExecutor.Execute(args.Item1, args.Item2))
            .Map(Renderer.Render)
            .Switch(
                s => Console.WriteLine(s),
                e => Console.Error.WriteLine($"Error: {e.Message}"))
            .Match(
                _ => 0,
                _ => 1);

        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        static Result<string, MdqError> ReadFile(string path)
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
}
