namespace Gma.Framework.Tenancy;

using Gma.Framework.Results;

public static class TenantErrors
{
    public static readonly Error TenantRequired = new("Tenancy.TenantRequired", "A tenant id is required.");
    public static readonly Error TenantInvalid = new("Tenancy.TenantInvalid", "The tenant id is not valid.");
}
