namespace Gma.Framework.Tenancy;

public interface ITenantContext
{
    bool IsEnabled { get; }
    string? TenantId { get; }
}
