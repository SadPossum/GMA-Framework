namespace Gma.Framework.Administration.Api;

using System.Security.Claims;
using Gma.Framework.Security;

public sealed class AdminApiOptions
{
    public const string SectionName = "Administration:Api";

    public string ActorIdClaim { get; set; } = ClaimTypes.NameIdentifier;
    public string TenantIdClaim { get; set; } = ApplicationClaimNames.TenantId;
    public bool RequireTenantClaimMatch { get; set; } = true;
    public bool AllowGeneratedPasswordResponses { get; set; }
}
