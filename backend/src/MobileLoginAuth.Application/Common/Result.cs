namespace MobileLoginAuth.Application.Common;

public enum ErrorType
{
    None,
    Validation,
    Unauthorized,
    Conflict,
    Locked,
    NotFound
}

public class Result
{
    protected Result(bool succeeded, ErrorType errorType, string? error)
    {
        Succeeded = succeeded;
        ErrorType = errorType;
        Error = error;
    }

    public bool Succeeded { get; }
    public ErrorType ErrorType { get; }
    public string? Error { get; }

    public static Result Success() => new(true, ErrorType.None, null);
    public static Result Failure(ErrorType type, string error) => new(false, type, error);
}

public sealed class Result<T> : Result
{
    private Result(T? value, bool succeeded, ErrorType errorType, string? error)
        : base(succeeded, errorType, error) => Value = value;

    public T? Value { get; }

    public static Result<T> Success(T value) => new(value, true, ErrorType.None, null);
    public static new Result<T> Failure(ErrorType type, string error) => new(default, false, type, error);
}
