namespace Gma.Framework.Tenancy;

public sealed class TenantOptions
{
    public const string SectionName = "Tenancy";

    public bool Enabled { get; set; }
    public string HeaderName { get; set; } = "X-Tenant-Id";
    public string LocalDefaultTenantId { get; set; } = "default";
}
