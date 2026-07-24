namespace Gma.Framework.AccessControl.AspNetCore;

using Microsoft.AspNetCore.Http;

internal sealed class AccessPermissionSetEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();
        AccessPermissionSetMetadata? metadata =
            endpoint?.Metadata.GetMetadata<AccessPermissionSetMetadata>();
        if (metadata is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        IResult? denied = await AccessPermissionEndpointFilter.AuthorizeAsync(
            context.HttpContext,
            metadata.Requirements).ConfigureAwait(false);
        return denied ?? await next(context).ConfigureAwait(false);
    }
}
