namespace Gma.Framework.Tests;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Gma.Framework.Api.Security;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ApiSecurityRegistrationTests
{
    [Fact]
    public void Api_security_defaults_reject_null_services()
    {
        Assert.Throws<ArgumentNullException>(() => ApiSecurityServiceCollectionExtensions.AddGmaApiSecurityDefaults(null!));
    }

    [Fact]
    public void Api_security_defaults_register_middleware_services_without_selecting_scheme()
    {
        ServiceCollection services = new();

        services.AddGmaApiSecurityDefaults();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IAuthenticationSchemeProvider>());
        Assert.NotNull(provider.GetRequiredService<IAuthorizationPolicyProvider>());
        Assert.Null(provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme);
    }
}
