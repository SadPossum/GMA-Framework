namespace Gma.Framework.Scoping;

public sealed class ScopeOptions
{
    public const string SectionName = "Scoping";

    public bool Enabled { get; set; }
    public string HeaderName { get; set; } = "X-Tenant-Id";
    public string LocalDefaultScopeId { get; set; } = "default";
}
