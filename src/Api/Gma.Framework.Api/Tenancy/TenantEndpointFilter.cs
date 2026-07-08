namespace Gma.Framework.Api.Tenancy;

using Gma.Framework.Naming;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Gma.Framework.Tenancy;

internal sealed class TenantEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        TenantOptions options = context.HttpContext.RequestServices.GetRequiredService<IOptions<TenantOptions>>().Value;

        if (!options.Enabled)
        {
            return await next(context).ConfigureAwait(false);
        }

        ITenantContextAccessor tenantContext =
            context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>();
        tenantContext.ClearTenant();

        if (!context.HttpContext.Request.Headers.TryGetValue(options.HeaderName, out StringValues headerValues))
        {
            return Results.Problem(
                title: TenantErrors.TenantRequired.Code,
                detail: TenantErrors.TenantRequired.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (headerValues.Count != 1)
        {
            return Results.Problem(
                title: TenantErrors.TenantInvalid.Code,
                detail: TenantErrors.TenantInvalid.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(headerValues[0]))
        {
            return Results.Problem(
                title: TenantErrors.TenantRequired.Code,
                detail: TenantErrors.TenantRequired.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TenantIds.TryNormalize(headerValues[0], out string? tenantId))
        {
            return Results.Problem(
                title: TenantErrors.TenantInvalid.Code,
                detail: TenantErrors.TenantInvalid.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        tenantContext.SetTenant(tenantId);

        return await next(context).ConfigureAwait(false);
    }
}
