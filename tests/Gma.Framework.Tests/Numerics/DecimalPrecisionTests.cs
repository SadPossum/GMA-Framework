namespace Gma.Framework.Tests;

using Gma.Framework.Numerics;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DecimalPrecisionTests
{
    [Fact]
    public void Fits_rejects_values_that_would_round_or_overflow_persistence_precision()
    {
        Assert.True(DecimalPrecision.Fits(9999999999999999.99m, precision: 18, scale: 2));
        Assert.True(DecimalPrecision.Fits(1.2300m, precision: 18, scale: 2));
        Assert.False(DecimalPrecision.Fits(1.234m, precision: 18, scale: 2));
        Assert.False(DecimalPrecision.Fits(10000000000000000m, precision: 18, scale: 2));
    }

    [Fact]
    public void Max_value_returns_largest_value_for_precision_and_scale()
    {
        Assert.Equal(9999999999999999.99m, DecimalPrecision.MaxValue(precision: 18, scale: 2));
    }
}
