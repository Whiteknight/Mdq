using AwesomeAssertions;
using Mdq.Cli.Arguments;
using Mdq.Core.Editing;

namespace Mdq.Tests.Arguments;

[TestFixture]
public class ArgumentParserEditModeTests
{
    // ------------------------------------------------------------------
    // Valid shapes
    // ------------------------------------------------------------------

    [Test]
    public void Add_WithoutInPlace_ReturnsEditModeWithAddOperation()
    {
        var result = ArgumentParser.Parse(["--add", "#Section", "file.md", "new text"]);

        result.Should().BeOfType<EditMode>()
            .Which.Operation.Should().BeOfType<Add>()
            .Which.Text.Should().Be("new text");
    }

    [Test]
    public void Set_WithoutInPlace_ReturnsEditModeWithSetOperation()
    {
        var result = ArgumentParser.Parse(["--set", "#Section", "file.md", "replacement"]);

        result.Should().BeOfType<EditMode>()
            .Which.Operation.Should().BeOfType<Set>()
            .Which.Text.Should().Be("replacement");
    }

    [Test]
    public void Add_WithInPlace_ReturnsEditModeWithInPlaceTrue()
    {
        var result = ArgumentParser.Parse(["--add", "--in-place", "#Section", "file.md", "new text"]);

        var mode = result.Should().BeOfType<EditMode>().Subject;
        mode.InPlace.Should().BeTrue();
        mode.Operation.Should().BeOfType<Add>().Which.Text.Should().Be("new text");
    }

    [Test]
    public void Set_WithInPlace_ReturnsEditModeWithInPlaceTrue()
    {
        var result = ArgumentParser.Parse(["--set", "--in-place", "#Section", "file.md", "replacement"]);

        var mode = result.Should().BeOfType<EditMode>().Subject;
        mode.InPlace.Should().BeTrue();
        mode.Operation.Should().BeOfType<Set>().Which.Text.Should().Be("replacement");
    }

    [Test]
    public void Add_WithoutInPlace_SetsCorrectSelectorAndFilePath()
    {
        var result = ArgumentParser.Parse(["--add", "#Intro.text", "docs/readme.md", "appended"]);

        var mode = result.Should().BeOfType<EditMode>().Subject;
        mode.Selector.Should().Be("#Intro.text");
        mode.FilePath.Should().Be("docs/readme.md");
        mode.InPlace.Should().BeFalse();
    }

    [Test]
    public void Set_WithInPlace_SetsCorrectSelectorAndFilePath()
    {
        var result = ArgumentParser.Parse(["--set", "--in-place", "#Title", "notes.md", "New Title"]);

        var mode = result.Should().BeOfType<EditMode>().Subject;
        mode.Selector.Should().Be("#Title");
        mode.FilePath.Should().Be("notes.md");
    }

    // ------------------------------------------------------------------
    // Missing arguments -> HelpMode
    // ------------------------------------------------------------------

    [Test]
    public void Add_MissingSelector_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--add"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Add_MissingFile_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--add", "#Section"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Add_MissingText_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--add", "#Section", "file.md"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Set_MissingSelector_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--set"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Set_MissingFile_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--set", "#Section"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Set_MissingText_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--set", "#Section", "file.md"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Add_InPlace_MissingSelector_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--add", "--in-place"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Test]
    public void Add_InPlace_MissingText_ReturnsHelpMode()
    {
        var result = ArgumentParser.Parse(["--add", "--in-place", "#Section", "file.md"]);

        result.Should().BeOfType<HelpMode>()
            .Which.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
