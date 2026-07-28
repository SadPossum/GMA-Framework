namespace Gma.Framework.Administration.Api;

using Gma.Framework.Administration;
using Microsoft.AspNetCore.Http;

public interface IAdminApiResourceScopeResolver
{
    bool TryResolve(
        HttpContext httpContext,
        out AdminResourceScope? resourceScope);
}
