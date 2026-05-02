using Mdq.Cli.Arguments;
using Mdq.Core.DocumentModel;
using Mdq.Core.Editing;
using Mdq.Core.QueryEngine;
using Mdq.Core.Rendering;
using Mdq.Core.SelectorModel;
using Mdq.Core.Shared;

namespace Mdq.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        return ArgumentParser.Parse(args) switch
        {
            HelpMode hm => PrintHelp(hm),
            TocMode tocm => PrintTableOfContents(tocm),
            QueryMode qm => ExtractQuery(qm),
            EditMode em => ExecuteEdit(em),
            _ => PrintHelp(new HelpMode { ErrorMessage = "Unknown mode. Use --help for usage instructions." }),
        };
    }

    private static int PrintHelp(HelpMode help)
    {
        var tw = Console.Out;
        if (!string.IsNullOrEmpty(help.ErrorMessage))
        {
            tw = Console.Error;
            tw.WriteLine($"Error: {help.ErrorMessage}");
            tw.WriteLine();
        }
        tw.WriteLine("""
        Usage: mdq <selector> <file>
               mdq --toc <file>
               mdq --add [--in-place] <selector> <file> <text>
               mdq --set [--in-place] <selector> <file> <text>

          <selector>  Query selector string (e.g. "#Introduction.text")
          <file>      Path to the Markdown file to query
          --toc       Only print headings, like a table of contents
          --add       Append text to the node(s) matched by <selector>
          --set       Replace the content of the node matched by <selector>
          --in-place  Write the result back to <file> instead of stdout

        Examples:
          mdq "" README.md
          mdq "#Installation" README.md
          mdq "#Usage.paragraph(1)" README.md
          mdq --add "#Installation.text" README.md "See also: CHANGELOG.md"
          mdq --set --in-place "#Introduction" README.md "Overview"
        """);

        return string.IsNullOrEmpty(help.ErrorMessage) ? 0 : 1;
    }

    private static int PrintTableOfContents(TocMode toc)
        => ExecuteSelectorAndFile(new TocRenderer(), ".flatten[type=heading]", toc.FilePath);

    private static int ExtractQuery(QueryMode query)
        => ExecuteSelectorAndFile(new MarkdownRenderer(), query.Selector, query.FilePath);

    private static int ExecuteSelectorAndFile(IRenderer renderer, string selector, string filePath)
        => ReadFile(filePath)
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

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static int ExecuteEdit(EditMode em)
        => ReadFile(em.FilePath)
            .Bind(MarkdownParser.Parse)
            .With(_ => SelectorParser.Parse(em.Selector))
            .Bind(pair => QueryExecutor.Execute(pair.Item1, pair.Item2)
                .Map(targets => (Doc: pair.Item1, Targets: targets)))
            .Bind(pair => EditValidator.Validate(pair.Targets, em.Operation)
                .MapError(e => (MdqError)e)
                .Map(targets => (pair.Doc, Targets: targets)))
            .Map(pair => RenderAllTargets(pair.Doc, pair.Targets, em.Operation))
            .Bind(rendered => WriteEditResult(rendered, em)).Switch(
                _ => { },
                e => Console.Error.WriteLine($"Error: {e.Message}"))
            .Match(_ => 0, _ => 1);

    private static string RenderAllTargets(
        MarkdownDocument document,
        IReadOnlyList<MatchableItem> targets,
        EditOperation operation)
        => new EditingMarkdownRenderer(targets, operation).Render(document);

    private static Result<Unit, MdqError> WriteEditResult(string rendered, EditMode em)
    {
        if (!em.InPlace)
        {
            Console.WriteLine(rendered);
            return new Unit();
        }

        try
        {
            File.WriteAllText(em.FilePath, rendered);
            return new Unit();
        }
        catch (IOException ex)
        {
            return new UnknownMdqError($"Could not write file '{em.FilePath}': {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new UnknownMdqError($"Access denied writing file '{em.FilePath}': {ex.Message}");
        }
    }

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
