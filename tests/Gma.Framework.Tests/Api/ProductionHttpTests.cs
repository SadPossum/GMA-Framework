namespace Gma.Framework.Tests.Api;

using Gma.Framework.Api.Production;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ProductionHttpTests
{
    [Fact]
    public void Validation_rejects_unrestricted_production_host_filtering()
    {
        string[] failures = ProductionHttpOptionsValidation.Validate(
            new ProductionHttpOptions(),
            isDevelopment: false,
            allowedHosts: "*");

        Assert.Contains(failures, failure => failure.Contains("AllowedHosts", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_accepts_safe_development_defaults()
    {
        string[] failures = ProductionHttpOptionsValidation.Validate(
            new ProductionHttpOptions(),
            isDevelopment: true,
            allowedHosts: "*");

        Assert.Empty(failures);
    }

    [Fact]
    public void Validation_requires_trusted_forwarder_configuration()
    {
        ProductionHttpOptions options = new()
        {
            ForwardedHeaders = new ForwardedHeadersSettings
            {
                Enabled = true
            }
        };

        string[] failures = ProductionHttpOptionsValidation.Validate(
            options,
            isDevelopment: true,
            allowedHosts: "*");

        Assert.Contains(failures, failure => failure.Contains("KnownProxies", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_accepts_a_trusted_forwarder_network()
    {
        ProductionHttpOptions options = new()
        {
            ForwardedHeaders = new ForwardedHeadersSettings
            {
                Enabled = true,
                KnownNetworks = ["10.42.0.0/16"]
            }
        };

        string[] failures = ProductionHttpOptionsValidation.Validate(
            options,
            isDevelopment: false,
            allowedHosts: "api.example.test");

        Assert.Empty(failures);
    }

    [Fact]
    public void Validation_rejects_unknown_forwarders_mixed_with_a_trust_list()
    {
        ProductionHttpOptions options = new()
        {
            ForwardedHeaders = new ForwardedHeadersSettings
            {
                Enabled = true,
                AllowUnknownProxies = true,
                KnownProxies = ["10.42.0.10"]
            }
        };

        string[] failures = ProductionHttpOptionsValidation.Validate(
            options,
            isDevelopment: false,
            allowedHosts: "api.example.test");

        Assert.Contains(
            failures,
            failure => failure.Contains("cannot combine", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_invalid_private_network_allowlist()
    {
        ProductionHttpOptions options = new()
        {
            PrivateNetwork = new PrivateNetworkSettings
            {
                Enabled = true,
                AllowedNetworks = ["not-a-network"]
            }
        };

        string[] failures = ProductionHttpOptionsValidation.Validate(
            options,
            isDevelopment: true,
            allowedHosts: "*");

        Assert.Contains(failures, failure => failure.Contains("AllowedNetworks", StringComparison.Ordinal));
    }

    [Fact]
    public void Registration_is_repeat_safe()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });

        builder.AddGmaProductionHttp();
        builder.AddGmaProductionHttp();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        ProductionHttpOptions options = provider.GetRequiredService<IOptions<ProductionHttpOptions>>().Value;

        Assert.True(options.RateLimiting.Enabled);
        Assert.Single(builder.Services, descriptor =>
            descriptor.ServiceType.Name.Contains("ProductionHttpRegistrationMarker", StringComparison.Ordinal));
        Assert.Collection(
            provider.GetServices<IExceptionHandler>(),
            handler => Assert.IsType<TransactionCoordinationExceptionHandler>(handler),
            handler => Assert.IsType<OptimisticConcurrencyExceptionHandler>(handler),
            handler => Assert.IsType<SanitizedUnhandledExceptionHandler>(handler));
    }

    [Fact]
    public void Distributed_mode_requires_a_distributed_provider_feature()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration["Http:RateLimiting:Mode"] = "Distributed";

        builder.AddGmaProductionHttp();

        ModuleCompositionValidationException exception =
            Assert.Throws<ModuleCompositionValidationException>(
                () => builder.ValidateModuleComposition());
        Assert.Contains(
            "rate-limiting.distributed-provider",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Distributed_policy_hashes_client_identity_and_applies_both_sensitive_budgets()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.42");
        context.Request.Path = "/api/auth/login";
        RateLimitingSettings settings = new()
        {
            GlobalPermitLimit = 100,
            SensitivePermitLimit = 5,
            WindowSeconds = 60,
            SensitivePathPrefixes = ["/api/auth"]
        };

        MultiPartitionRateLimitRequest request =
            HttpRateLimitPolicy.CreateDistributedRequest(context, settings);

        Assert.StartsWith("http-", request.AtomicGroup, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.42", request.AtomicGroup, StringComparison.Ordinal);
        Assert.Collection(
            request.Partitions,
            general =>
            {
                Assert.Equal("http-general", general.Identity);
                Assert.Equal(100, general.PermitLimit);
            },
            sensitive =>
            {
                Assert.Equal("http-sensitive", sensitive.Identity);
                Assert.Equal(5, sensitive.PermitLimit);
            });
    }

    [Fact]
    public async Task Distributed_middleware_fails_closed_when_the_provider_is_unavailable()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();
        DefaultHttpContext context = new()
        {
            RequestServices = services
        };
        context.Response.Body = new MemoryStream();
        bool nextCalled = false;
        DistributedHttpRateLimitMiddleware middleware = new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new StubRateLimiter(MultiPartitionRateLimitDecision.ProviderUnavailable()),
            Options.Create(new ProductionHttpOptions
            {
                RateLimiting = new RateLimitingSettings
                {
                    Mode = HttpRateLimitMode.Distributed
                }
            }));

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("1", context.Response.Headers.RetryAfter);
        context.Response.Body.Position = 0;
        string response = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("Http.RateLimitProviderUnavailable", response, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_data_protection_requires_a_persistent_key_ring()
    {
        string[] failures = ProductionDataProtectionOptionsValidation.Validate(
            new ProductionDataProtectionOptions
            {
                ApplicationName = "example"
            },
            applicationNamespace: null,
            isProduction: true);

        Assert.Contains(
            failures,
            failure => failure.Contains("KeyRingPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Data_protection_composition_uses_the_application_namespace_fallback()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Development
        });
        builder.Configuration["ApplicationIdentity:Namespace"] = "example";

        builder.AddGmaProductionDataProtection();
        builder.AddGmaProductionDataProtection();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<IDataProtectionProvider>());
        Assert.Single(builder.Services, descriptor =>
            descriptor.ServiceType.Name.Contains(
                "ProductionDataProtectionRegistrationMarker",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Unhandled_exception_handler_exposes_only_a_stable_problem_and_trace_identifier()
    {
        const string exceptionCanary = "private.person@example.test failed for reservation-sensitive-491";
        CapturingProblemDetailsService problemDetailsService = new();
        SanitizedUnhandledExceptionHandler handler = new(problemDetailsService);
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "0HMTESTTRACE0001"
        };

        bool handled = await handler.TryHandleAsync(
            httpContext,
            new InvalidOperationException(exceptionCanary),
            CancellationToken.None);

        ProblemDetailsContext captured = Assert.IsType<ProblemDetailsContext>(problemDetailsService.Context);
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.Equal("Http.UnexpectedFailure", captured.ProblemDetails.Title);
        Assert.Equal("0HMTESTTRACE0001", captured.ProblemDetails.Extensions["traceId"]);
        Assert.Null(captured.Exception);
        Assert.DoesNotContain(exceptionCanary, captured.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transaction_coordination_handler_returns_a_sanitized_retryable_response()
    {
        CapturingProblemDetailsService problemDetailsService = new();
        TransactionCoordinationExceptionHandler handler = new(problemDetailsService);
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "0HMTESTTRACE0002"
        };

        bool handled = await handler.TryHandleAsync(
            httpContext,
            new TransactionCoordinationException(
                TransactionCoordinationFailure.DeadlockVictim),
            CancellationToken.None);

        ProblemDetailsContext captured = Assert.IsType<ProblemDetailsContext>(problemDetailsService.Context);
        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
        Assert.Equal("1", httpContext.Response.Headers.RetryAfter);
        Assert.Equal("Persistence.CoordinationUnavailable", captured.ProblemDetails.Title);
        Assert.Equal("0HMTESTTRACE0002", captured.ProblemDetails.Extensions["traceId"]);
        Assert.DoesNotContain("deadlock", captured.ProblemDetails.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Null(captured.Exception);
    }

    [Fact]
    public void Default_sensitive_rate_limit_covers_all_authentication_entry_points()
    {
        string[] prefixes = new ProductionHttpOptions().RateLimiting.SensitivePathPrefixes;

        Assert.Contains("/api/auth/browser", prefixes, StringComparer.Ordinal);
        Assert.Contains("/api/auth/password", prefixes, StringComparer.Ordinal);
        Assert.Contains("/api/auth/external", prefixes, StringComparer.Ordinal);
        Assert.Contains("/api/auth/email-verification", prefixes, StringComparer.Ordinal);
    }

    [Fact]
    public void Readiness_registration_is_tagged_separately_from_liveness()
    {
        ServiceCollection services = new();
        services.AddGmaReadinessCheck(
            "database",
            (_, _) => Task.FromResult(HealthCheckResult.Healthy()));

        using ServiceProvider provider = services.BuildServiceProvider();
        HealthCheckServiceOptions options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration registration = Assert.Single(options.Registrations);

        Assert.Contains(GmaHealthCheckTags.Readiness, registration.Tags);
    }

    private sealed class CapturingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetailsContext? Context { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            this.Context = context;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            this.Context = context;
            return ValueTask.FromResult(true);
        }
    }

    private sealed class StubRateLimiter(MultiPartitionRateLimitDecision decision)
        : IMultiPartitionRateLimiter
    {
        public ValueTask<MultiPartitionRateLimitDecision> AcquireAsync(
            MultiPartitionRateLimitRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(decision);
    }
}
