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
}
