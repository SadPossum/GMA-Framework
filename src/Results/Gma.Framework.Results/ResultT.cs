namespace Gma.Framework.Results;

public sealed class Result<TValue> : Result
{
    private readonly TValue? value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        if (isSuccess && value is null)
        {
            throw new InvalidOperationException("A successful result value cannot be null.");
        }

        this.value = value;
    }

    public TValue Value =>
        this.IsSuccess
            ? this.value ?? throw new InvalidOperationException("Successful result value cannot be null.")
            : throw new InvalidOperationException("Failed result value cannot be accessed.");
}
