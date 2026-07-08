namespace Gma.Framework.Caching.Infrastructure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Gma.Framework.Caching;

internal sealed class HybridCacheMetricsLoggerProvider(
    CacheMetrics metrics,
    IOptions<CachingOptions> options) : ILoggerProvider
{
    private const string HybridCacheCategory = "Microsoft.Extensions.Caching.Hybrid.HybridCache";
    private readonly ILogger logger = new HybridCacheMetricsLogger(
        metrics,
        options.Value.Provider.ToString().ToLowerInvariant());

    public ILogger CreateLogger(string categoryName) =>
        string.Equals(categoryName, HybridCacheCategory, StringComparison.Ordinal)
            ? this.logger
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class HybridCacheMetricsLogger(CacheMetrics metrics, string provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string? operation = eventId.Id switch
            {
                6 => "read",
                7 => "write",
                11 => "data-rejected",
                _ => null
            };

            if (operation is not null)
            {
                try
                {
                    metrics.RecordBackendFailure("unknown", operation, provider);
                }
                catch (Exception)
                {
                    // Metrics are observability only; listener/exporter failures must not affect cache logging.
                }
            }
        }
    }
}
