namespace Gma.Framework.RateLimiting.Redis;

using StackExchange.Redis;

internal sealed record RedisRateLimitingConnectionSettings(
    ConfigurationOptions Configuration);

internal sealed class RedisRateLimitingConnection(
    RedisRateLimitingConnectionSettings settings)
    : IAsyncDisposable
{
    private readonly Lock gate = new();
    private Task<ConnectionMultiplexer>? connectionTask;
    private bool disposed;

    public async ValueTask<IConnectionMultiplexer> GetAsync(
        CancellationToken cancellationToken)
    {
        Task<ConnectionMultiplexer> pending;
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            pending = this.connectionTask ??=
                ConnectionMultiplexer.ConnectAsync(settings.Configuration);
        }

        try
        {
            return await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            lock (this.gate)
            {
                if (ReferenceEquals(this.connectionTask, pending))
                {
                    this.connectionTask = null;
                }
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task<ConnectionMultiplexer>? pending;
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            pending = this.connectionTask;
        }

        if (pending is null)
        {
            return;
        }

        try
        {
            ConnectionMultiplexer multiplexer =
                await pending.ConfigureAwait(false);
            await multiplexer.CloseAsync(allowCommandsToComplete: false)
                .ConfigureAwait(false);
            multiplexer.Dispose();
        }
        catch (RedisException)
        {
        }
    }
}
