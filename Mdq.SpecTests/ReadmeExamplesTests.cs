using AwesomeAssertions;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Mdq.Core.DocumentModel;
using Mdq.Core.QueryEngine;
using Mdq.Core.Rendering;
using Mdq.Core.SelectorModel;
using NUnit.Framework;

namespace Mdq.SpecTests;

[TestFixture]
public sealed class ReadmeExamplesTests
{
    private static string ReadmePath => FindReadme();

    private static string FindReadme()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.sln").Any())
            dir = dir.Parent;

        if (dir is null)
            throw new FileNotFoundException("Could not locate solution root from " + AppContext.BaseDirectory);

        var path = Path.Combine(dir.FullName, "README.md");
        if (!File.Exists(path))
            throw new FileNotFoundException("README.md not found at " + path);

        return path;
    }

    public static IEnumerable<TestCaseData> ReadmeExamples()
    {
        var markdown = File.ReadAllText(ReadmePath);
        var doc = Markdig.Markdown.Parse(markdown);
        var blocks = doc.ToList();

        bool inExamplesSection = false;

        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i] is HeadingBlock h)
            {
                inExamplesSection = h.Level == 2 && ExtractText(h) == "Examples";
                continue;
            }

            if (!inExamplesSection)
                continue;

            var selector = TryExtractSelector(blocks[i]);
            if (selector is null)
                continue;

            if (i + 1 < blocks.Count && blocks[i + 1] is Markdig.Syntax.FencedCodeBlock fcb && fcb.Info == "markdown")
                yield return new TestCaseData(selector, fcb.Lines.ToString().Trim()).SetName(selector);
        }
    }

    private static string? TryExtractSelector(Block block)
    {
        if (block is not ParagraphBlock para)
            return null;

        // Paragraph must start with bold "Selector:" followed by a code inline
        var inlines = para.Inline?.ToList() ?? [];
        if (inlines.Count < 2)
            return null;

        if (inlines[0] is not EmphasisInline bold || bold.DelimiterCount != 2)
            return null;

        var boldText = string.Concat(bold.OfType<LiteralInline>().Select(l => l.Content.ToString()));
        if (boldText != "Selector:")
            return null;

        var codeInline = inlines.Skip(1).OfType<CodeInline>().FirstOrDefault();
        return codeInline?.Content;
    }

    private static string ExtractText(HeadingBlock h)
        => string.Concat(
            h.Inline?.OfType<LiteralInline>().Select(l => l.Content.ToString()) ?? []);

    [TestCaseSource(nameof(ReadmeExamples))]
    public void ExampleProducesExpectedOutput(string selector, string expectedOutput)
    {
        var readmeText = File.ReadAllText(ReadmePath);

        var document = MarkdownParser.Parse(readmeText)
            .Match(d => d, e => throw new Exception(e.Message));

        var chain = SelectorParser.Parse(selector)
            .Match(c => c, e => throw new Exception(e.Message));

        var results = QueryExecutor.Execute(document, chain)
            .Match(r => r, e => throw new Exception(e.Message));

        var output = new MarkdownRenderer().Render(results).Trim().ReplaceLineEndings("\n");

        output.Should().Be(expectedOutput.ReplaceLineEndings("\n"));
    }
}
