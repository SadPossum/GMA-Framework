namespace Gma.Framework.RateLimiting.Redis;

using Gma.Framework.RateLimiting;
using StackExchange.Redis;

internal sealed class RedisMultiPartitionRateLimiter(
    RedisRateLimitingConnection connection,
    RedisRateLimitStorageKeyFormatter keyFormatter)
    : IMultiPartitionRateLimiter
{
    private const string AcquireScript = """
        local permit_count = tonumber(ARGV[1])
        if not permit_count or permit_count <= 0 or #KEYS == 0 then
            return {-1, 0}
        end

        local server_time = redis.call('TIME')
        local now_ms = (tonumber(server_time[1]) * 1000) + math.floor(tonumber(server_time[2]) / 1000)
        local counts = {}
        local buckets = {}
        local ttls = {}
        local max_retry_ms = 0

        for index = 1, #KEYS do
            local argument_index = 2 + ((index - 1) * 2)
            local permit_limit = tonumber(ARGV[argument_index])
            local window_ms = tonumber(ARGV[argument_index + 1])
            if not permit_limit or not window_ms or permit_limit <= 0 or window_ms <= 0 then
                return {-1, 0}
            end

            local bucket = math.floor(now_ms / window_ms)
            local ttl_ms = window_ms - (now_ms % window_ms)
            local count = 0
            local raw = redis.call('GET', KEYS[index])

            if raw then
                local separator = string.find(raw, ':', 1, true)
                if not separator then
                    return {-1, 0}
                end

                local stored_bucket = tonumber(string.sub(raw, 1, separator - 1))
                local stored_count = tonumber(string.sub(raw, separator + 1))
                if not stored_bucket or not stored_count or stored_count < 0 then
                    return {-1, 0}
                end

                if stored_bucket == bucket then
                    count = stored_count
                end
            end

            counts[index] = count
            buckets[index] = bucket
            ttls[index] = ttl_ms

            if count + permit_count > permit_limit and ttl_ms > max_retry_ms then
                max_retry_ms = ttl_ms
            end
        end

        if max_retry_ms > 0 then
            return {0, max_retry_ms}
        end

        for index = 1, #KEYS do
            local next_count = counts[index] + permit_count
            redis.call(
                'PSETEX',
                KEYS[index],
                ttls[index],
                tostring(buckets[index]) .. ':' .. tostring(next_count))
        end

        return {1, 0}
        """;

    public async ValueTask<MultiPartitionRateLimitDecision> AcquireAsync(
        MultiPartitionRateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        RedisKey[] keys = request.Partitions
            .Select(partition => keyFormatter.Format(request.AtomicGroup, partition))
            .ToArray();
        RedisValue[] arguments = BuildArguments(request);

        try
        {
            IConnectionMultiplexer multiplexer =
                await connection.GetAsync(cancellationToken).ConfigureAwait(false);
            IDatabase database = multiplexer.GetDatabase();
            RedisResult result = await database
                .ScriptEvaluateAsync(AcquireScript, keys, arguments)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            return MapResult(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RedisException)
        {
            return MultiPartitionRateLimitDecision.ProviderUnavailable();
        }
        catch (TimeoutException)
        {
            return MultiPartitionRateLimitDecision.ProviderUnavailable();
        }
    }

    private static RedisValue[] BuildArguments(MultiPartitionRateLimitRequest request)
    {
        RedisValue[] arguments = new RedisValue[1 + (request.Partitions.Count * 2)];
        arguments[0] = request.PermitCount;

        for (int index = 0; index < request.Partitions.Count; index++)
        {
            FixedWindowRateLimitPartition partition = request.Partitions[index];
            arguments[1 + (index * 2)] = partition.PermitLimit;
            arguments[2 + (index * 2)] = checked((long)partition.Window.TotalMilliseconds);
        }

        return arguments;
    }

    private static MultiPartitionRateLimitDecision MapResult(RedisResult result)
    {
        if (result.Length != 2)
        {
            return MultiPartitionRateLimitDecision.ProviderUnavailable();
        }

        long status = (long)result[0];
        long retryAfterMilliseconds = (long)result[1];

        return status switch
        {
            1 when retryAfterMilliseconds == 0 =>
                MultiPartitionRateLimitDecision.Acquired(),
            0 when retryAfterMilliseconds > 0 &&
                retryAfterMilliseconds <=
                    FixedWindowRateLimitPartition.MaximumWindow.TotalMilliseconds =>
                MultiPartitionRateLimitDecision.Rejected(
                    TimeSpan.FromMilliseconds(retryAfterMilliseconds)),
            _ => MultiPartitionRateLimitDecision.ProviderUnavailable()
        };
    }
}
