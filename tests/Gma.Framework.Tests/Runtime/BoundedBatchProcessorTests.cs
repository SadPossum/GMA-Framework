namespace Gma.Framework.Tests;

using Gma.Framework.Runtime.Maintenance;
using Xunit;

public sealed class BoundedBatchProcessorTests
{
    [Fact]
    public async Task ExecuteAsync_stops_after_a_partial_batch()
    {
        Queue<int> batches = new([3, 3, 1, 3]);

        int processed = await BoundedBatchProcessor.ExecuteAsync(
            batchSize: 3,
            maximumBatches: 4,
            (_, _) => Task.FromResult(batches.Dequeue()),
            CancellationToken.None);

        Assert.Equal(7, processed);
        Assert.Single(batches);
    }

    [Fact]
    public async Task ExecuteAsync_honors_the_batch_limit()
    {
        int invocations = 0;

        int processed = await BoundedBatchProcessor.ExecuteAsync(
            batchSize: 5,
            maximumBatches: 2,
            (_, _) =>
            {
                invocations++;
                return Task.FromResult(5);
            },
            CancellationToken.None);

        Assert.Equal(10, processed);
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_an_invalid_batch_result()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => BoundedBatchProcessor.ExecuteAsync(
            batchSize: 5,
            maximumBatches: 2,
            (_, _) => Task.FromResult(6),
            CancellationToken.None));
    }
}
