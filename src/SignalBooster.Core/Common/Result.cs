namespace SignalBooster.Core.Common;

public readonly record struct Success;

public static class Result
{
    public static Success Success => default;
    
    public static Result<TValue> CreateSuccess<TValue>(TValue value) => new(value);
    
    public static Result<TValue> CreateFailure<TValue>(Error error) => new(error);
    
    public static Result<TValue> CreateFailure<TValue>(List<Error> errors) => new(errors);
}

public interface IResult<out TValue>
{
    bool IsSuccess { get; }
    bool IsError { get; }
    List<Error> Errors { get; }
    TValue? Value { get; }
    Error FirstError { get; }
}

public readonly partial record struct Result<TValue> : IResult<TValue>
{
    private readonly TValue? _value = default;

    public Result(TValue value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _value = value;
    }

    public Result(Error error)
    {
        Errors = [error];
    }

    public Result(List<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("Cannot create a Result<TValue> from an empty collection of errors. Provide at least one error.", nameof(errors));
        }

        Errors = errors;
    }

    public bool IsSuccess => Errors is null or [];

    public bool IsError => Errors?.Count > 0;

    public List<Error> Errors { get; } = [];

    public TValue? Value
    {
        get
        {
            if (IsError)
            {
                throw new InvalidOperationException("The Value property cannot be accessed when Errors property is not empty. Check IsSuccess or IsError before accessing the Value.");
            }

            return _value;
        }
    }

    public Error FirstError
    {
        get
        {
            if (!IsError)
            {
                throw new InvalidOperationException("The FirstError property cannot be accessed when Errors property is empty. Check IsError before accessing FirstError.");
            }

            return Errors[0];
        }
    }

    public static implicit operator Result<TValue>(TValue value) => new(value);
    public static implicit operator Result<TValue>(Error error) => new(error);
    public static implicit operator Result<TValue>(List<Error> errors) => new(errors);
}