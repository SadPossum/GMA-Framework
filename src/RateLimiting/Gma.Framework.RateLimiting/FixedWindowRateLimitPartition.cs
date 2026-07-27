namespace Gma.Framework.RateLimiting;

public sealed record FixedWindowRateLimitPartition
{
    public const int IdentityMaxLength = 512;
    public const int PermitLimitMaxValue = 1_000_000_000;
    public static readonly TimeSpan MinimumWindow = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(31);

    public FixedWindowRateLimitPartition(
        string identity,
        int permitLimit,
        TimeSpan window)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);

        string normalizedIdentity = identity.Trim();
        if (normalizedIdentity.Length > IdentityMaxLength ||
            normalizedIdentity.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"Rate-limit partition identities must be {IdentityMaxLength} characters or fewer and cannot contain whitespace or control characters.",
                nameof(identity));
        }

        if (permitLimit is <= 0 or > PermitLimitMaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permitLimit),
                $"Rate-limit partition permit limits must be between 1 and {PermitLimitMaxValue}.");
        }

        if (window < MinimumWindow ||
            window > MaximumWindow ||
            window.Ticks % TimeSpan.TicksPerMillisecond != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(window),
                $"Fixed windows must be whole milliseconds between {MinimumWindow} and {MaximumWindow}.");
        }

        this.Identity = normalizedIdentity;
        this.PermitLimit = permitLimit;
        this.Window = window;
    }

    public string Identity { get; }
    public int PermitLimit { get; }
    public TimeSpan Window { get; }
}
