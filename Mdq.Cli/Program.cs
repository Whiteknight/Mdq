using Mdq.Core.DocumentModel;
using Mdq.Core.QueryEngine;
using Mdq.Core.Shared;
using Mdq.Core.SelectorModel;

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

var readResult = ReadFile(filePath);
if (readResult is Result<string, string>.Err fileErr)
{
    Console.Error.WriteLine(fileErr.Error);
    return 1;
}

var fileContent = ((Result<string, string>.Ok)readResult).Value;

var parseResult = MarkdownParser.Parse(fileContent);
if (parseResult is Result<MarkdownDocument, MarkdownParseError>.Err parseErr)
{
    Console.Error.WriteLine($"Markdown parse error: {parseErr.Error.Message}");
    return 1;
}

var document = ((Result<MarkdownDocument, MarkdownParseError>.Ok)parseResult).Value;

var selectorResult = SelectorParser.Parse(selectorArg);
if (selectorResult is Result<SelectorChain, SelectorParseError>.Err selectorErr)
{
    Console.Error.WriteLine($"Selector parse error at position {selectorErr.Error.Position}: {selectorErr.Error.Message}");
    return 1;
}

var chain = ((Result<SelectorChain, SelectorParseError>.Ok)selectorResult).Value;

var queryResult = QueryExecutor.Execute(document, chain);
if (queryResult is Result<string, QueryError>.Err queryErr)
{
    Console.Error.WriteLine($"Query error: {queryErr.Error.Message}");
    return 1;
}

var output = ((Result<string, QueryError>.Ok)queryResult).Value;
Console.WriteLine(output);
return 0;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

static Result<string, string> ReadFile(string path)
{
    if (!File.Exists(path))
        return new Result<string, string>.Err($"File not found: {path}");

    try
    {
        return new Result<string, string>.Ok(File.ReadAllText(path));
    }
    catch (IOException ex)
    {
        return new Result<string, string>.Err($"Could not read file '{path}': {ex.Message}");
    }
    catch (UnauthorizedAccessException ex)
    {
        return new Result<string, string>.Err($"Access denied reading file '{path}': {ex.Message}");
    }
}
