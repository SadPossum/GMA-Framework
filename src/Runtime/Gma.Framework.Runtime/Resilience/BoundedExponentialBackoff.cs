namespace Gma.Framework.Runtime.Resilience;

public static class BoundedExponentialBackoff
{
    public static TimeSpan Calculate(
        int attempt,
        TimeSpan baseDelay,
        TimeSpan maximumDelay,
        int maximumExponent = 10)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        if (baseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
        }

        if (maximumDelay < baseDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDelay),
                "Maximum delay must be greater than or equal to the base delay.");
        }

        if (maximumExponent is < 0 or > 30)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumExponent),
                "Maximum exponent must be between 0 and 30.");
        }

        int exponent = Math.Clamp(attempt - 1, 0, maximumExponent);
        double calculatedTicks = baseDelay.Ticks * Math.Pow(2, exponent);
        long boundedTicks = calculatedTicks >= maximumDelay.Ticks
            ? maximumDelay.Ticks
            : (long)calculatedTicks;
        return TimeSpan.FromTicks(boundedTicks);
    }
}
