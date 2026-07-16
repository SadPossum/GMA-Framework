namespace Gma.Framework.Api.Tenancy;

using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

public static class TenantEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder RequireTenant(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter<TenantEndpointFilter>();
    }

    /// <summary>
    /// Requires a valid tenant while allowing the endpoint to perform its own authentication.
    /// Registered tenant access policies are not evaluated for this endpoint.
    /// </summary>
    /// <remarks>
    /// Use this only when the endpoint authenticates the caller before performing protected work.
    /// </remarks>
    public static RouteHandlerBuilder RequireTenantWithIndependentAuthentication(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithMetadata(IndependentTenantEndpointAuthenticationMetadata.Instance)
            .AddEndpointFilter<TenantEndpointFilter>();
    }
}
