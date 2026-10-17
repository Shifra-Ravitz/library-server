using System.Diagnostics.CodeAnalysis;

namespace Library.Application.Common;

/// <summary>
/// The outcome of a service operation that returns no value: success, or an <see cref="Common.Error"/>.
/// Business failures are returned, not thrown; exceptions are kept for unexpected failures.
/// </summary>
public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    /// <summary>
    /// Null when the operation succeeded.
    /// </summary>
    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => Error is not null;

    public static Result Success() => new(null);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// The outcome of a service operation that returns a <typeparamref name="T"/> on success.
/// </summary>
/// <remarks>
/// C# never applies a user-defined conversion from an interface type, so when T is an interface
/// (e.g. <c>IReadOnlyList&lt;X&gt;</c>) the implicit operator from T is ignored; use <see cref="Success(T)"/> explicitly.
/// </remarks>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>
    /// The value of a successful result. Reading it on a failure is a programming error.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
