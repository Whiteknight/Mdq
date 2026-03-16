using AwesomeAssertions;
using Mdq.Core.Shared;

namespace Mdq.Tests.Shared;

[TestFixture]
public class ResultTests
{
    private sealed record TestError(string Message) : MdqError(Message);

    [Test]
    public void Ok_Value_ReturnsWrappedValue()
    {
        var result = new Result<int, TestError>.Ok(42);

        result.Should().BeOfType<Result<int, TestError>.Ok>()
            .Which.Value.Should().Be(42);
    }

    [Test]
    public void Err_Error_ReturnsWrappedError()
    {
        var error = new TestError("something went wrong");
        var result = new Result<int, TestError>.Err(error);

        result.Should().BeOfType<Result<int, TestError>.Err>()
            .Which.Error.Should().Be(error);
    }

    [Test]
    public void Map_OnOk_TransformsValue()
    {
        var result = new Result<int, TestError>.Ok(10);

        var mapped = result.Map(v => v * 2);

        mapped.Should().BeOfType<Result<int, TestError>.Ok>()
            .Which.Value.Should().Be(20);
    }

    [Test]
    public void Map_OnErr_PropagatesError()
    {
        var error = new TestError("fail");
        var result = new Result<int, TestError>.Err(error);

        var mapped = result.Map(v => v * 2);

        mapped.Should().BeOfType<Result<int, TestError>.Err>()
            .Which.Error.Should().Be(error);
    }

    [Test]
    public void Bind_OnOk_AppliesFunction()
    {
        var result = new Result<int, TestError>.Ok(5);

        var bound = result.Bind(v => new Result<string, TestError>.Ok(v.ToString()));

        bound.Should().BeOfType<Result<string, TestError>.Ok>()
            .Which.Value.Should().Be("5");
    }

    [Test]
    public void Bind_OnOk_CanReturnErr()
    {
        var result = new Result<int, TestError>.Ok(5);
        var error = new TestError("bound to fail");

        var bound = result.Bind<string>(_ => new Result<string, TestError>.Err(error));

        bound.Should().BeOfType<Result<string, TestError>.Err>()
            .Which.Error.Should().Be(error);
    }

    [Test]
    public void Bind_OnErr_PropagatesError()
    {
        var error = new TestError("original");
        var result = new Result<int, TestError>.Err(error);

        var bound = result.Bind(v => new Result<string, TestError>.Ok(v.ToString()));

        bound.Should().BeOfType<Result<string, TestError>.Err>()
            .Which.Error.Should().Be(error);
    }

    [Test]
    public void MdqError_Message_IsAccessible()
    {
        var error = new TestError("test message");

        error.Message.Should().Be("test message");
    }
}
