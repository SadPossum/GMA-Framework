namespace Gma.Framework.Runtime;

using Gma.Framework.Naming;

public sealed class ApplicationIdentityOptions
{
    public const string SectionName = "ApplicationIdentity";
    public const string DefaultDisplayName = "GenericModularApi";

    public string DisplayName { get; set; } = DefaultDisplayName;
    public string Namespace { get; set; } = ApplicationNamespaces.Default;

    public string EffectiveDisplayName =>
        string.IsNullOrWhiteSpace(this.DisplayName)
            ? DefaultDisplayName
            : this.DisplayName.Trim();

    public string EffectiveNamespace => ApplicationNamespaces.Normalize(this.Namespace);
}
