namespace Gma.Framework.AccessControl.AspNetCore;

using Gma.Framework.AccessControl;
using Gma.Framework.Security;

public sealed class AccessControlAspNetCoreOptions
{
    public const string SectionName = "AccessControl:AspNetCore";

    public AccessSubjectKind DefaultSubjectKind { get; set; } = AccessSubjectKind.User;
    public string SubjectClaimName { get; set; } = GmaClaimNames.Subject;
}
