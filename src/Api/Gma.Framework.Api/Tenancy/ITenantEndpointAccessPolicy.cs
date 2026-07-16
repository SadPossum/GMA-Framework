namespace Gma.Framework.Api.Tenancy;

using Microsoft.AspNetCore.Http;

public interface ITenantEndpointAccessPolicy
{
    ValueTask<TenantEndpointAccessDecision> AuthorizeAsync(
        HttpContext httpContext,
        string tenantId,
        CancellationToken cancellationToken);
}
