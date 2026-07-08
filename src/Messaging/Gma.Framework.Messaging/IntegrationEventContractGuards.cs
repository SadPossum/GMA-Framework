namespace Gma.Framework.Messaging;

using Gma.Framework.Naming;
using Gma.Framework.Numerics;

public static class IntegrationEventContractGuards
{
    public static Guid RequireId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value;
    }

    public static DateTimeOffset RequireOccurredAtUtc(DateTimeOffset value, string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value;
    }

    public static string NormalizeEventName(string eventName, string parameterName) =>
        IntegrationEventNaming.NormalizeEventName(eventName, parameterName);

    public static int RequireVersion(int value, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, parameterName);

        return value;
    }

    public static string NormalizeRequiredText(string value, int maxLength, string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim();
        if (normalized.Length > maxLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{parameterName} must be {maxLength} characters or fewer and cannot contain control characters.",
                parameterName);
        }

        return normalized;
    }

    public static decimal RequirePositiveDecimal(
        decimal value,
        int precision,
        int scale,
        string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{parameterName} must be greater than zero.", parameterName);
        }

        if (!DecimalPrecision.Fits(value, precision, scale))
        {
            throw new ArgumentException($"{parameterName} must fit the configured decimal precision.", parameterName);
        }

        return value;
    }

    public static int RequireNonNegative(int value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{parameterName} must not be negative.");
        }

        return value;
    }

    public static TEnum NormalizeDefinedOrUnknown<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        Enum.IsDefined(value) ? value : default;
}
