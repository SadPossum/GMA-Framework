namespace Gma.Framework.Api.Scoping;

using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

internal sealed class ScopeEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        IScopeContext scopeContext =
            context.HttpContext.RequestServices.GetRequiredService<IScopeContext>();
        if (!scopeContext.IsEnabled)
        {
            return await next(context).ConfigureAwait(false);
        }

        ScopeOptions options = context.HttpContext.RequestServices.GetRequiredService<IOptions<ScopeOptions>>().Value;
        IScopeContextAccessor scopeAccessor =
            context.HttpContext.RequestServices.GetRequiredService<IScopeContextAccessor>();
        scopeAccessor.ClearScope();

        if (!context.HttpContext.Request.Headers.TryGetValue(options.HeaderName, out StringValues headerValues))
        {
            return Results.Problem(
                title: ScopeErrors.ScopeRequired.Code,
                detail: ScopeErrors.ScopeRequired.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (headerValues.Count != 1)
        {
            return Results.Problem(
                title: ScopeErrors.ScopeInvalid.Code,
                detail: ScopeErrors.ScopeInvalid.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(headerValues[0]))
        {
            return Results.Problem(
                title: ScopeErrors.ScopeRequired.Code,
                detail: ScopeErrors.ScopeRequired.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!ScopeIds.TryNormalize(headerValues[0], out string? scopeId))
        {
            return Results.Problem(
                title: ScopeErrors.ScopeInvalid.Code,
                detail: ScopeErrors.ScopeInvalid.Message,
                statusCode: StatusCodes.Status400BadRequest);
        }

        scopeAccessor.SetScope(scopeId);

        return await next(context).ConfigureAwait(false);
    }
}
