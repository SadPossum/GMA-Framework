namespace Gma.Framework.Tests.Observability;

using System.Globalization;
using global::Serilog;
using global::Serilog.Core;
using global::Serilog.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Serilog;
using Gma.Framework.Observability;

public sealed class SafeRequestLoggingTests
{
    [Fact]
    public async Task Request_logging_uses_route_templates_and_omits_exception_details()
    {
        const string guestIdentifierCanary = "guest-sensitive-491";
        const string queryCanary = "guest491@example.test";
        const string exceptionCanary = "guest 491 passport could not be loaded";

        TestLogEventSink sink = new();
        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        ServiceCollection serviceCollection = new();
        serviceCollection.AddSingleton<ILogger>(logger);
        await using ServiceProvider services = serviceCollection.BuildServiceProvider();
        RequestDelegate pipeline = BuildPipeline(services, _ => throw new InvalidOperationException(exceptionCanary));
        DefaultHttpContext context = CreateContext(services, guestIdentifierCanary, queryCanary);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline(context));

        LogEvent logEvent = Assert.Single(sink.Events);
        string renderedEvent = logEvent.RenderMessage(CultureInfo.InvariantCulture);

        Assert.Equal(exceptionCanary, exception.Message);
        Assert.Null(logEvent.Exception);
        Assert.Equal(LogEventLevel.Error, logEvent.Level);
        Assert.Equal("guests", ScalarValue(logEvent, ObservabilityLogPropertyNames.Module));
        Assert.Equal("InvalidOperationException", ScalarValue(logEvent, "ExceptionType"));
        Assert.Equal("/guests/{guestId}", ScalarValue(logEvent, "RouteTemplate"));
        Assert.DoesNotContain(guestIdentifierCanary, renderedEvent, StringComparison.Ordinal);
        Assert.DoesNotContain(queryCanary, renderedEvent, StringComparison.Ordinal);
        Assert.DoesNotContain(exceptionCanary, renderedEvent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Request_logging_accepts_only_bounded_contributor_dimensions()
    {
        const string personalDataCanary = "private.person@example.test";

        TestLogEventSink sink = new();
        using Logger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();
        ServiceCollection serviceCollection = new();
        serviceCollection.AddSingleton<ILogger>(logger);
        serviceCollection.AddSingleton<IRequestLoggingDiagnosticContextContributor>(
            new TestContributor(personalDataCanary));
        await using ServiceProvider services = serviceCollection.BuildServiceProvider();
        RequestDelegate pipeline = BuildPipeline(services, _ => Task.CompletedTask);
        DefaultHttpContext context = CreateContext(services, "guest-1", "safe@example.test");

        await pipeline(context);

        LogEvent logEvent = Assert.Single(sink.Events);

        Assert.Equal(true, ScalarValue(logEvent, ObservabilityLogPropertyNames.TenantScoped));
        Assert.Equal("reservation-create", ScalarValue(logEvent, ObservabilityLogPropertyNames.Operation));
        Assert.False(logEvent.Properties.ContainsKey(ObservabilityLogPropertyNames.Subject));
        Assert.False(logEvent.Properties.ContainsKey("ExceptionType"));
        Assert.DoesNotContain(
            personalDataCanary,
            logEvent.RenderMessage(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static RequestDelegate BuildPipeline(
        IServiceProvider services,
        RequestDelegate terminal)
    {
        ApplicationBuilder builder = new(services);
        builder.UseGmaSerilogRequestLogging();
        builder.Run(terminal);

        return builder.Build();
    }

    private static DefaultHttpContext CreateContext(
        IServiceProvider services,
        string guestIdentifier,
        string queryValue)
    {
        DefaultHttpContext context = new()
        {
            RequestServices = services,
            TraceIdentifier = "0HMTESTTRACE0001",
        };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = $"/guests/{guestIdentifier}";
        context.Request.QueryString = new QueryString($"?email={queryValue}");
        context.SetEndpoint(new RouteEndpoint(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/guests/{guestId}"),
            order: 0,
            new EndpointMetadataCollection(new ModuleEndpointMetadata("Guests")),
            displayName: "Get guest"));

        return context;
    }

    private static object? ScalarValue(LogEvent logEvent, string propertyName) =>
        Assert.IsType<ScalarValue>(logEvent.Properties[propertyName]).Value;

    private sealed class TestContributor(string personalDataCanary)
        : IRequestLoggingDiagnosticContextContributor
    {
        public void Enrich(IDiagnosticContext diagnosticContext, HttpContext httpContext)
        {
            diagnosticContext.Set(ObservabilityLogPropertyNames.TenantScoped, value: true);
            diagnosticContext.Set(ObservabilityLogPropertyNames.Operation, "reservation-create");
            diagnosticContext.Set(ObservabilityLogPropertyNames.Subject, personalDataCanary);
            diagnosticContext.SetException(new InvalidOperationException(personalDataCanary));
        }
    }

    private sealed class TestLogEventSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => this.Events.Add(logEvent);
    }
}
