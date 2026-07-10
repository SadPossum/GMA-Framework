namespace Gma.Framework.Api.Scoping;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class ScopeEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder RequireScope(this RouteHandlerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddEndpointFilter<ScopeEndpointFilter>();
    }
}
