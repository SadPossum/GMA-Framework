namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Microsoft.AspNetCore.Http;

public interface IAccessHttpSubjectResolver
{
    AccessSubject? ResolveSubject(HttpContext httpContext);
}
