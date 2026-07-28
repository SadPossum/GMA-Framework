namespace Gma.Framework.Tests;

using System.Diagnostics.Metrics;
using System.Text.Json;
using Gma.Framework.Observability;
using Gma.Framework.Observability.Infrastructure;
using Gma.Framework.Runtime;
using Gma.Framework.Runtime.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
[Collection(MetricsTestGroupDefinition.Name)]
public sealed class SecuritySignalObservabilityTests
{
    private static readonly SecuritySignalDefinition Definition = new(
        "access-control.permission-denied",
        SecuritySignalCategory.Authorization,
        SecuritySignalSeverity.Warning);

    [Fact]
    public void Definition_requires_bounded_lowercase_static_semantics()
    {
        _ = new SecuritySignalDefinition(
            "auth.password-proof-rate-limited",
            SecuritySignalCategory.Authentication,
            SecuritySignalSeverity.Warning);

        Assert.Throws<ArgumentException>(() =>
            new SecuritySignalDefinition(
                "Auth.Dynamic",
                SecuritySignalCategory.Authentication,
                SecuritySignalSeverity.Warning));
        Assert.Throws<ArgumentException>(() =>
            new SecuritySignalDefinition(
                "missing-dot",
                SecuritySignalCategory.Authentication,
                SecuritySignalSeverity.Warning));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SecuritySignalDefinition(
                "auth.invalid-category",
                SecuritySignalCategory.Unknown,
                SecuritySignalSeverity.Warning));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SecuritySignalDefinition(
                "auth.invalid-severity",
                SecuritySignalCategory.Authentication,
                (SecuritySignalSeverity)99));
    }

    [Fact]
    public void Record_serializes_only_the_payload_free_evidence_fields()
    {
        SecuritySignalRecord record = new(
            Definition,
            "0123456789abcdef0123456789abcdef",
            new DateTimeOffset(2026, 7, 28, 4, 30, 0, TimeSpan.Zero));

        JsonElement json = JsonSerializer.SerializeToElement(record);
        string[] propertyNames = json
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Category",
                "IncidentCorrelationId",
                "OccurredAtUtc",
                "Severity",
                "SignalCode"
            ],
            propertyNames);
        Assert.DoesNotContain(
            json.EnumerateObject(),
            property => property.Name.Contains("payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Core_registration_is_optional_and_returns_a_stable_noop_receipt()
    {
        ServiceCollection services = [];
        services.AddSecuritySignalCore();
        services.AddSecuritySignalCore();
        await using ServiceProvider provider = services.BuildServiceProvider();
        Guid correlationId = Guid.Parse("019c061e-7700-7000-8000-000000000001");

        SecuritySignalReceipt receipt = provider
            .GetRequiredService<ISecuritySignalRecorder>()
            .Record(Definition, correlationId);

        Assert.Equal(correlationId.ToString("N"), receipt.IncidentCorrelationId);
        Assert.False(receipt.WasEmitted);
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(ISecuritySignalRecorder));
    }

    [Fact]
    public async Task Recorder_emits_bounded_metrics_and_correlated_structured_logs()
    {
        List<MetricMeasurement> measurements = [];
        using MeterListener listener = CreateListener(measurements);
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        CapturingLogger<SecuritySignalRecorder> logger = new();
        SecuritySignalRecorder recorder = CreateRecorder(
            provider.GetRequiredService<IMeterFactory>(),
            logger);
        Guid correlationId = Guid.Parse("019c061e-7700-7000-8000-000000000002");

        SecuritySignalReceipt receipt = recorder.Record(Definition, correlationId);

        Assert.True(receipt.WasEmitted);
        Assert.Equal(correlationId.ToString("N"), receipt.IncidentCorrelationId);
        MetricMeasurement metric = Assert.Single(measurements);
        Assert.Equal(ObservabilityInstrumentNames.SecuritySignals, metric.InstrumentName);
        Assert.Equal(Definition.Code, metric.Tags[ObservabilityTagNames.SecuritySignal]);
        Assert.Equal(
            SecuritySignalCategories.Authorization,
            metric.Tags[ObservabilityTagNames.SecurityCategory]);
        Assert.Equal(
            SecuritySignalSeverities.Warning,
            metric.Tags[ObservabilityTagNames.SecuritySeverity]);
        Assert.DoesNotContain(
            metric.Tags.Keys,
            key => key.Contains("correlation", StringComparison.OrdinalIgnoreCase));

        IReadOnlyDictionary<string, object?> log = Assert.Single(logger.Entries);
        Assert.Equal(Definition.Code, log[ObservabilityLogPropertyNames.SecuritySignalCode]);
        Assert.Equal(
            SecuritySignalCategories.Authorization,
            log[ObservabilityLogPropertyNames.SecuritySignalCategory]);
        Assert.Equal(
            SecuritySignalSeverities.Warning,
            log[ObservabilityLogPropertyNames.SecuritySignalSeverity]);
        Assert.Equal(
            correlationId.ToString("N"),
            log[ObservabilityLogPropertyNames.IncidentCorrelationId]);
        Assert.DoesNotContain(
            log.Keys,
            key => key.Contains("tenant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            log.Keys,
            key => key.Contains("payload", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Recorder_rejects_unknown_or_conflicting_definitions()
    {
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        SecuritySignalRecorder recorder = CreateRecorder(
            provider.GetRequiredService<IMeterFactory>(),
            NullLogger<SecuritySignalRecorder>.Instance);

        Assert.Throws<InvalidOperationException>(() =>
            recorder.Record(new(
                "auth.password-proof-rate-limited",
                SecuritySignalCategory.Authentication,
                SecuritySignalSeverity.Warning)));
        Assert.Throws<InvalidOperationException>(() =>
            recorder.Record(new(
                Definition.Code,
                Definition.Category,
                SecuritySignalSeverity.Critical)));
    }

    [Fact]
    public void Registry_rejects_duplicate_definition_codes()
    {
        SecuritySignalDefinition duplicate = new(
            Definition.Code,
            Definition.Category,
            Definition.Severity);

        Assert.Throws<InvalidOperationException>(() =>
            new SecuritySignalRegistry(
            [
                new DefinitionSource(Definition),
                new DefinitionSource(duplicate)
            ]));
    }

    [Fact]
    public async Task Recorder_fails_open_when_metric_and_logger_providers_throw()
    {
        using MeterListener listener = CreateThrowingListener();
        ServiceCollection services = [];
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();
        SecuritySignalRecorder recorder = CreateRecorder(
            provider.GetRequiredService<IMeterFactory>(),
            new ThrowingLogger<SecuritySignalRecorder>());

        SecuritySignalReceipt receipt = recorder.Record(
            Definition,
            Guid.Parse("019c061e-7700-7000-8000-000000000003"));

        Assert.False(receipt.WasEmitted);
    }

    private static SecuritySignalRecorder CreateRecorder(
        IMeterFactory meterFactory,
        ILogger<SecuritySignalRecorder> logger) =>
        new(
            new SecuritySignalRegistry([new DefinitionSource(Definition)]),
            new SecuritySignalMetrics(meterFactory, ApplicationIdentity()),
            new TestClock(),
            logger);

    private static IOptions<ApplicationIdentityOptions> ApplicationIdentity() =>
        Options.Create(new ApplicationIdentityOptions { Namespace = "gma" });

    private static MeterListener CreateListener(
        ICollection<MetricMeasurement> measurements)
    {
        MeterListener listener = new()
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == ObservabilityMeterNames.Application)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            measurements.Add(new(
                instrument.Name,
                value,
                tags.ToArray().ToDictionary(
                    item => item.Key,
                    item => item.Value,
                    StringComparer.Ordinal))));
        listener.Start();
        return listener;
    }

    private static MeterListener CreateThrowingListener()
    {
        MeterListener listener = new()
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == ObservabilityMeterNames.Application)
                {
                    currentListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, _, _) =>
            throw new InvalidOperationException("Metric listener unavailable."));
        listener.Start();
        return listener;
    }

    private sealed class DefinitionSource(params SecuritySignalDefinition[] definitions)
        : ISecuritySignalDefinitionSource
    {
        public IReadOnlyCollection<SecuritySignalDefinition> Definitions { get; } =
            definitions;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 7, 28, 4, 30, 0, TimeSpan.Zero);
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        long Value,
        Dictionary<string, object?> Tags);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<IReadOnlyDictionary<string, object?>> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                this.Entries.Add(
                    properties.ToDictionary(
                        item => item.Key,
                        item => item.Value,
                        StringComparer.Ordinal));
            }
        }
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException("Logger unavailable.");
    }
}
