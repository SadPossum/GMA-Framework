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

    [Fact]
    public async Task ExecuteAsync_observes_completed_batches_before_a_later_failure()
    {
        int observed = 0;
        int invocations = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => BoundedBatchProcessor.ExecuteAsync(
            batchSize: 5,
            maximumBatches: 3,
            (_, _) => ++invocations == 1
                ? Task.FromResult(5)
                : Task.FromException<int>(new InvalidOperationException("cleanup failed")),
            processed => observed += processed,
            CancellationToken.None));

        Assert.Equal(5, observed);
        Assert.Equal(2, invocations);
    }
}
