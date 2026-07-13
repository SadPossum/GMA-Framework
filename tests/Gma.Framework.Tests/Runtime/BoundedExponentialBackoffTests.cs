namespace Gma.Framework.Tests;

using Gma.Framework.Runtime.Resilience;
using Xunit;

public sealed class BoundedExponentialBackoffTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(20, 300)]
    public void Calculate_returns_bounded_exponential_delay(int attempt, int expectedSeconds)
    {
        TimeSpan delay = BoundedExponentialBackoff.Calculate(
            attempt,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(5),
            maximumExponent: 8);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void Calculate_rejects_invalid_bounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedExponentialBackoff.Calculate(
            attempt: 0,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedExponentialBackoff.Calculate(
            attempt: 1,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedExponentialBackoff.Calculate(
            attempt: 1,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1)));
    }
}
