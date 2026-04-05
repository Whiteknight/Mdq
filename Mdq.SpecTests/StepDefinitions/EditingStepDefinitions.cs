using AwesomeAssertions;
using Mdq.Core.DocumentModel;
using Mdq.Core.Editing;
using Mdq.Core.QueryEngine;
using Mdq.Core.Rendering;
using Mdq.Core.SelectorModel;
using Mdq.Core.Shared;

namespace Mdq.SpecTests.StepDefinitions;

[Binding]
public sealed class EditingStepDefinitions
{
    private readonly ScenarioContext _context;

    public EditingStepDefinitions(ScenarioContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // Given
    // -------------------------------------------------------------------------

    [Given("I have markdown text for editing:")]
    public void IHaveMarkdownTextForEditing(string text)
    {
        _context["edit_markdown"] = text;
        _context["edit_document"] = ParseOrThrow(text);
    }

    [Given("a temporary file containing:")]
    public void ATempFileContaining(string text)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, text);
        _context["temp_file_path"] = path;
    }

    // -------------------------------------------------------------------------
    // When -- successful edits
    // -------------------------------------------------------------------------

    [When("I add {string} to selector {string}")]
    public void IAddTextToSelector(string text, string selector)
        => ExecuteEdit(selector, new Add(text));

    [When("I set {string} on selector {string}")]
    public void ISetTextOnSelector(string text, string selector)
        => ExecuteEdit(selector, new Set(text));

    // -------------------------------------------------------------------------
    // When -- expected failures
    // -------------------------------------------------------------------------

    [When("I attempt to add {string} to selector {string}")]
    public void IAttemptToAddTextToSelector(string text, string selector)
        => ExecuteEditCapturingError(selector, new Add(text));

    [When("I attempt to set {string} on selector {string}")]
    public void IAttemptToSetTextOnSelector(string text, string selector)
        => ExecuteEditCapturingError(selector, new Set(text));

    // -------------------------------------------------------------------------
    // When -- in-place
    // -------------------------------------------------------------------------

    [When("I run --add --in-place {string} on selector {string} against the temp file")]
    public void IRunAddInPlace(string text, string selector)
        => ExecuteInPlaceEdit(selector, new Add(text));

    [When("I attempt --add --in-place {string} on selector {string} against the temp file")]
    public void IAttemptAddInPlace(string text, string selector)
        => ExecuteInPlaceEditCapturingError(selector, new Add(text));

    // -------------------------------------------------------------------------
    // Then -- output assertions
    // -------------------------------------------------------------------------

    [Then("the edited document should contain {string}")]
    public void TheEditedDocumentShouldContain(string expected)
    {
        var output = _context.Get<string>("edit_output");
        output.Should().Contain(expected);
    }

    [Then("the edited document should not contain {string}")]
    public void TheEditedDocumentShouldNotContain(string unexpected)
    {
        var output = _context.Get<string>("edit_output");
        output.Should().NotContain(unexpected);
    }

    [Then("the edit should fail with error containing {string}")]
    public void TheEditShouldFailWithErrorContaining(string fragment)
    {
        var error = _context.Get<string>("edit_error");
        error.Should().Contain(fragment);
    }

    [Then("the temp file should contain {string}")]
    public void TheTempFileShouldContain(string expected)
    {
        var path = _context.Get<string>("temp_file_path");
        File.ReadAllText(path).Should().Contain(expected);
    }

    [Then("the temp file should not contain {string}")]
    public void TheTempFileShouldNotContain(string unexpected)
    {
        var path = _context.Get<string>("temp_file_path");
        File.ReadAllText(path).Should().NotContain(unexpected);
    }

    // -------------------------------------------------------------------------
    // Cleanup
    // -------------------------------------------------------------------------

    [AfterScenario]
    public void CleanupTempFile()
    {
        if (!_context.TryGetValue("temp_file_path", out var pathObj))
            return;

        var path = (string)pathObj;
        if (File.Exists(path))
            File.Delete(path);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void ExecuteEdit(string selector, EditOperation operation)
    {
        var doc = _context.Get<MarkdownDocument>("edit_document");
        var targets = ResolveTargets(doc, selector);
        var validated = EditValidator.Validate(targets, operation)
            .Match(
                t => t,
                e => throw new InvalidOperationException($"Unexpected validation error: {e.Message}"));

        var output = new EditingMarkdownRenderer(validated, operation).Render(doc);
        _context["edit_output"] = output;
    }

    private void ExecuteEditCapturingError(string selector, EditOperation operation)
    {
        var doc = _context.Get<MarkdownDocument>("edit_document");

        var errorMessage = TryResolveTargetsForError(doc, selector, out var targets)
            ?? TryValidateForError(targets, operation);

        _context["edit_error"] = errorMessage
            ?? throw new InvalidOperationException("Expected an error but the operation succeeded.");
    }

    private void ExecuteInPlaceEdit(string selector, EditOperation operation)
    {
        var path = _context.Get<string>("temp_file_path");
        var markdown = File.ReadAllText(path);
        var doc = ParseOrThrow(markdown);
        var targets = ResolveTargets(doc, selector);
        var validated = EditValidator.Validate(targets, operation)
            .Match(
                t => t,
                e => throw new InvalidOperationException($"Unexpected validation error: {e.Message}"));

        var rendered = new EditingMarkdownRenderer(validated, operation).Render(doc);
        File.WriteAllText(path, rendered);
    }

    private void ExecuteInPlaceEditCapturingError(string selector, EditOperation operation)
    {
        var path = _context.Get<string>("temp_file_path");
        var originalContent = File.ReadAllText(path);
        var doc = ParseOrThrow(originalContent);

        var errorMessage = TryResolveTargetsForError(doc, selector, out var targets)
            ?? TryValidateForError(targets!, operation);

        // Error occurred -- file must not be modified
        errorMessage.Should().NotBeNull("expected an error but the operation succeeded");
        // File is left untouched because we never reached the write step
    }

    private static IReadOnlyList<MatchableItem> ResolveTargets(MarkdownDocument doc, string selector)
    {
        var chain = SelectorParser.Parse(selector)
            .Match(c => c, e => throw new InvalidOperationException($"Selector parse error: {e.Message}"));

        return QueryExecutor.Execute(doc, chain)
            .Match(r => r, e => throw new InvalidOperationException($"Query error: {e.Message}"));
    }

    /// <summary>
    /// Attempts to resolve targets, returning an error message if selector parsing or query execution fails.
    /// Sets <paramref name="targets"/> to the resolved list on success, or an empty list on failure.
    /// </summary>
    private static string? TryResolveTargetsForError(
        MarkdownDocument doc,
        string selector,
        out IReadOnlyList<MatchableItem> targets)
    {
        var chainResult = SelectorParser.Parse(selector);
        if (!chainResult.IsSuccess)
        {
            targets = [];
            return chainResult.GetErrorOrDefault().Message;
        }

        var queryResult = QueryExecutor.Execute(doc, chainResult.GetValueOrDefault());
        if (!queryResult.IsSuccess)
        {
            targets = [];
            return queryResult.GetErrorOrDefault().Message;
        }

        targets = queryResult.GetValueOrDefault();
        return null;
    }

    /// <summary>
    /// Attempts validation, returning an error message if validation fails, or null if it succeeds.
    /// </summary>
    private static string? TryValidateForError(IReadOnlyList<MatchableItem> targets, EditOperation operation)
    {
        var result = EditValidator.Validate(targets, operation);
        return result.Match(_ => (string?)null, e => e.Message);
    }

    private static MarkdownDocument ParseOrThrow(string markdown)
        => MarkdownParser.Parse(markdown)
            .Match(d => d, e => throw new InvalidOperationException($"Parse error: {e.Message}"));
}
