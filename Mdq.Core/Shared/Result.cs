namespace Mdq.Core.Shared;

public abstract record Result<T, E>
{
    public sealed record Ok(T Value) : Result<T, E>;
    public sealed record Err(E Error) : Result<T, E>;

    public Result<U, E> Map<U>(Func<T, U> f) => this switch
    {
        Ok(var v) => new Result<U, E>.Ok(f(v)),
        Err(var e) => new Result<U, E>.Err(e),
        _ => throw new InvalidOperationException()
    };

    public Result<U, E> Bind<U>(Func<T, Result<U, E>> f) => this switch
    {
        Ok(var v) => f(v),
        Err(var e) => new Result<U, E>.Err(e),
        _ => throw new InvalidOperationException()
    };
}
