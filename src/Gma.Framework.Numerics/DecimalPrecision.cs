namespace Gma.Framework.Numerics;

public static class DecimalPrecision
{
    public const int MaxSupportedPrecision = 28;

    public static bool Fits(decimal value, int precision, int scale)
    {
        Validate(precision, scale);

        if (value == decimal.MinValue)
        {
            return false;
        }

        decimal rounded = decimal.Round(value, scale, MidpointRounding.ToZero);
        if (value != rounded)
        {
            return false;
        }

        return Math.Abs(value) <= MaxValue(precision, scale);
    }

    public static decimal MaxValue(int precision, int scale)
    {
        Validate(precision, scale);

        decimal value = 0;
        for (int index = 0; index < precision; index++)
        {
            value = (value * 10) + 9;
        }

        for (int index = 0; index < scale; index++)
        {
            value /= 10;
        }

        return value;
    }

    private static void Validate(int precision, int scale)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(precision, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(precision, MaxSupportedPrecision);
        ArgumentOutOfRangeException.ThrowIfLessThan(scale, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(scale, precision);
    }
}
