namespace Gma.Framework.RateLimiting;

public sealed record MultiPartitionRateLimitRequest
{
    public const int AtomicGroupMaxLength = 256;
    public const int MaxPartitions = 8;
    public const int PermitCountMaxValue = FixedWindowRateLimitPartition.PermitLimitMaxValue;

    public MultiPartitionRateLimitRequest(
        string atomicGroup,
        int permitCount,
        IEnumerable<FixedWindowRateLimitPartition> partitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(atomicGroup);
        ArgumentNullException.ThrowIfNull(partitions);

        string normalizedAtomicGroup = atomicGroup.Trim();
        if (normalizedAtomicGroup.Length > AtomicGroupMaxLength ||
            normalizedAtomicGroup.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"Atomic rate-limit groups must be {AtomicGroupMaxLength} characters or fewer and cannot contain whitespace or control characters.",
                nameof(atomicGroup));
        }

        if (permitCount is <= 0 or > PermitCountMaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(permitCount),
                $"Permit counts must be between 1 and {PermitCountMaxValue}.");
        }

        FixedWindowRateLimitPartition[] values = partitions.ToArray();
        if (values.Length is 0 or > MaxPartitions)
        {
            throw new ArgumentException(
                $"Atomic rate-limit requests must contain between 1 and {MaxPartitions} partitions.",
                nameof(partitions));
        }

        if (values.Any(partition => partition is null))
        {
            throw new ArgumentException("Rate-limit partitions cannot contain null values.", nameof(partitions));
        }

        if (values
            .GroupBy(partition => partition.Identity, StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Rate-limit partition identities must be unique within an atomic request.",
                nameof(partitions));
        }

        if (values.Any(partition => permitCount > partition.PermitLimit))
        {
            throw new ArgumentException(
                "The requested permit count cannot exceed any partition permit limit.",
                nameof(permitCount));
        }

        this.AtomicGroup = normalizedAtomicGroup;
        this.PermitCount = permitCount;
        this.Partitions = Array.AsReadOnly(values);
    }

    public string AtomicGroup { get; }
    public int PermitCount { get; }
    public IReadOnlyList<FixedWindowRateLimitPartition> Partitions { get; }
}
