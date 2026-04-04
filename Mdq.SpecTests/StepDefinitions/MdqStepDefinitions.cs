using AwesomeAssertions;
using Mdq.Core.DocumentModel;
using Mdq.Core.QueryEngine;
using Mdq.Core.Rendering;
using Mdq.Core.SelectorModel;

namespace Mdq.SpecTests.StepDefinitions;

[Binding]
public sealed record MdqStepDefinitions(ScenarioContext Context)
{
    [Given("I have markdown text:")]
    public void IHaveMarkdownText(string text)
    {
        Context["markdown"] = text;
        Context["document"] = MarkdownParser.Parse(text).Match(d => d, e => throw new Exception(e.Message));
    }

    [When("I execute selector {string}")]
    public void IExecuteSelectorString(string selector)
    {
        var doc = Context.Get<MarkdownDocument>("document");

        var chain = SelectorParser.Parse(selector).Match(c => c, e => throw new Exception(e.Message));
        Context["chain"] = chain;

        var results = QueryExecutor.Execute(doc, chain).Match(r => r, e => throw new Exception(e.Message));
        Context["result"] = results;

        var output = new MarkdownRenderer().Render(results).Trim();
        Context["output"] = output;
    }

    [Then("The result text should be:")]
    public void TheResultTextShouldBe(string expected)
    {
        var output = Context.Get<string>("output");
        output.Should().Be(expected.Trim());
    }
}
