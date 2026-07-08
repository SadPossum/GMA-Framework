namespace Gma.Framework.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DisableTenantFilterAttribute(string reason) : Attribute
{
    public string Reason { get; } = string.IsNullOrWhiteSpace(reason)
        ? throw new ArgumentException("A tenant filter disable reason is required.", nameof(reason))
        : reason.Trim();
}
