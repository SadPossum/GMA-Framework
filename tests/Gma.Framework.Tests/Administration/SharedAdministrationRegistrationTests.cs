namespace Gma.Framework.Tests;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Administration.Cli;
using Xunit;

[Trait("Category", "Unit")]
public sealed class SharedAdministrationRegistrationTests
{
    [Fact]
    public void Shared_administration_registration_rejects_null_arguments()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddGmaAdministration(null!));
        Assert.Throws<ArgumentNullException>(() =>
            Administration.Cli.DependencyInjection.AddGmaAdministrationCli(null!));
        Assert.Throws<ArgumentNullException>(() =>
            Administration.Api.DependencyInjection.AddGmaAdministrationApi(null!, configuration));
        Assert.Throws<ArgumentNullException>(() =>
            new ServiceCollection().AddGmaAdministrationApi(null!));
    }

    [Fact]
    public void Shared_administration_cli_registration_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddGmaAdministrationCli();
        services.AddGmaAdministrationCli();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AdminCliGlobalOptions));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AdminCliExecutor));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAdminOperationRunner));
    }

    [Fact]
    public void Shared_administration_api_registration_is_idempotent()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();

        services.AddGmaAdministrationApi(configuration);
        services.AddGmaAdministrationApi(configuration);

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(AdminApiExecutor));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConfigureOptions<AdminApiOptions>));
        Assert.Single(services, HasService<IValidateOptions<AdminApiOptions>, AdminApiOptionsValidator>());
        Assert.Single(services, descriptor => descriptor.ServiceType.Name == "AdminApiOptionsRegistrationMarker");
    }

    [Theory]
    [InlineData("Administration:Api:ActorIdClaim", "actor id", "ActorIdClaim")]
    [InlineData("Administration:Api:TenantIdClaim", "tenant id", "TenantIdClaim")]
    public void Shared_administration_api_rejects_invalid_options_at_composition(
        string setting,
        string value,
        string expectedFailure)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [setting] = value
            })
            .Build();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddGmaAdministrationApi(configuration));

        Assert.Contains(exception.Failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Fact]
    public void Shared_administration_api_rejects_missing_tenant_claim_when_claim_match_is_required_at_composition()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Administration:Api:RequireTenantClaimMatch"] = "true",
                ["Administration:Api:TenantIdClaim"] = string.Empty
            })
            .Build();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddGmaAdministrationApi(configuration));

        Assert.Contains(exception.Failures, failure => failure.Contains("TenantIdClaim", StringComparison.Ordinal));
    }

    private static Predicate<ServiceDescriptor> HasService<TService, TImplementation>() =>
        descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation);
}
