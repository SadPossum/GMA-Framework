namespace Gma.Framework.Tests.Api;

using Gma.Framework.Api.Production;
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
}
