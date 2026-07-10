namespace Gma.Framework.AccessControl.AspNetCore;

using System.Security.Claims;
using Gma.Framework.AccessControl;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

internal sealed class ClaimsAccessHttpSubjectResolver(IOptions<AccessControlAspNetCoreOptions> options)
    : IAccessHttpSubjectResolver
{
    public AccessSubject? ResolveSubject(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        ClaimsPrincipal user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        AccessControlAspNetCoreOptions currentOptions = options.Value;
        string? subjectId = user.FindFirstValue(currentOptions.SubjectClaimName) ??
            user.FindFirstValue(ClaimTypes.NameIdentifier);

        return AccessSubject.TryCreate(
            currentOptions.DefaultSubjectKind,
            subjectId,
            out AccessSubject? subject)
            ? subject
            : null;
    }
}
