namespace Gma.Framework.Domain;

public interface ITenantScoped
{
    string TenantId { get; }
}
