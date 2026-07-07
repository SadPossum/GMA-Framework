namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Naming;

public sealed record RequiredCompositionModule
{
    public RequiredCompositionModule(
        string moduleName,
        string owner,
        bool optional = false,
        string? reason = null)
    {
        this.ModuleName = SharedModuleNames.Normalize(moduleName, nameof(moduleName));
        this.Owner = CompositionText.RequireSafeText(owner, nameof(owner));
        this.Optional = optional;
        this.Reason = CompositionText.OptionalSafeText(reason, nameof(reason));
    }

    public string ModuleName { get; }
    public string Owner { get; }
    public bool Optional { get; }
    public string? Reason { get; }
}
