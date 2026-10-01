namespace PatternsLab.Domain.Common;

public enum ResultStatus
{
    Success = 0,
    NotFound = 1,
    Conflict = 2,
    Invalid = 3,
    Gone = 4
}

/// <summary>
/// A use case's outcome expressed as a value instead of an exception, so the API layer
/// can map it to a status code without try/catch and without knowing about EF Core.
/// </summary>
public class Result
{
    protected Result(ResultStatus status, string? error) => (Status, Error) = (status, error);

    public ResultStatus Status { get; }
    public string? Error { get; }
    public bool IsSuccess => Status == ResultStatus.Success;

    public static Result Success() => new(ResultStatus.Success, null);
    public static Result NotFound(string error) => new(ResultStatus.NotFound, error);
    public static Result Conflict(string error) => new(ResultStatus.Conflict, error);
    public static Result Invalid(string error) => new(ResultStatus.Invalid, error);
    public static Result Gone(string error) => new(ResultStatus.Gone, error);
}

public sealed class Result<T> : Result
{
    private Result(ResultStatus status, T? value, string? error) : base(status, error) => Value = value;

    public T? Value { get; }

    public static Result<T> Success(T value) => new(ResultStatus.Success, value, null);
    public new static Result<T> NotFound(string error) => new(ResultStatus.NotFound, default, error);
    public new static Result<T> Conflict(string error) => new(ResultStatus.Conflict, default, error);
    public new static Result<T> Invalid(string error) => new(ResultStatus.Invalid, default, error);
    public new static Result<T> Gone(string error) => new(ResultStatus.Gone, default, error);
}
