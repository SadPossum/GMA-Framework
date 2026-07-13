namespace Gma.Framework.Runtime.Maintenance;

public static class BoundedBatchProcessor
{
    public static async Task<int> ExecuteAsync(
        int batchSize,
        int maximumBatches,
        Func<int, CancellationToken, Task<int>> processBatch,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumBatches, 1);
        ArgumentNullException.ThrowIfNull(processBatch);

        int processedTotal = 0;
        for (int batch = 0; batch < maximumBatches; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int processed = await processBatch(batchSize, cancellationToken).ConfigureAwait(false);
            if (processed < 0 || processed > batchSize)
            {
                throw new InvalidOperationException(
                    $"Bounded batch processor received invalid batch count {processed}; expected 0 through {batchSize}.");
            }

            processedTotal = checked(processedTotal + processed);
            if (processed < batchSize)
            {
                break;
            }
        }

        return processedTotal;
    }
}
