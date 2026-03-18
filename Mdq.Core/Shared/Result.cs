namespace Mdq.Core.Shared;

public abstract record Result<T, E>
{
    public bool IsSuccess => this is Ok;
    public bool IsError => this is Err;

    public sealed record Ok(T Value) : Result<T, E>;
    public sealed record Err(E Error) : Result<T, E>;

    public static implicit operator Result<T, E>(T value) => new Ok(value);
    public static implicit operator Result<T, E>(E error) => new Err(error);

    public T GetValueOrDefault(T defaultValue = default!)
        => this switch
        {
            Ok(var v) => v,
            Err => defaultValue,
            _ => throw new InvalidOperationException()
        };

    public E GetErrorOrDefault(E defaultError = default!)
        => this switch
        {
            Ok => defaultError,
            Err(var e) => e,
            _ => throw new InvalidOperationException()
        };

    public Result<U, E> Map<U>(Func<T, U> f)
        => this switch
        {
            Ok(var v) => new Result<U, E>.Ok(f(v)),
            Err(var e) => new Result<U, E>.Err(e),
            _ => throw new InvalidOperationException()
        };

    public Result<U, E> Bind<U>(Func<T, Result<U, E>> f)
        => this switch
        {
            Ok(var v) => f(v),
            Err(var e) => new Result<U, E>.Err(e),
            _ => throw new InvalidOperationException()
        };

    public Result<T, F> MapError<F>(Func<E, F> f)
        => this switch
        {
            Ok(var v) => new Result<T, F>.Ok(v),
            Err(var e) => new Result<T, F>.Err(f(e)),
            _ => throw new InvalidOperationException()
        };

    public Result<(T, T2), E> With<T2>(Func<T, Result<T2, E>> getOther)
    {
        if (this is Err e1)
            return new Result<(T, T2), E>.Err(e1.Error);
        var ok1 = this as Ok;
        var v1 = ok1!.Value;
        var inner = getOther(v1);
        if (inner is Err e2)
            return new Result<(T, T2), E>.Err(e2.Error);

        var v2 = (inner as Result<T2, E>.Ok)!.Value;
        return new Result<(T, T2), E>.Ok((v1!, v2!));
    }

    public Result<T, E> Switch(Action<T> onValue, Action<E> onError)
    {
        if (this is Ok(var v))
            onValue(v);
        else if (this is Err(var e))
            onError(e);
        else
            throw new InvalidOperationException();
        return this;
    }

    public TOut Match<TOut>(Func<T, TOut> onValue, Func<E, TOut> onError)
        => this switch
        {
            Ok(var v) => onValue(v),
            Err(var e) => onError(e),
            _ => throw new InvalidOperationException()
        };
}
