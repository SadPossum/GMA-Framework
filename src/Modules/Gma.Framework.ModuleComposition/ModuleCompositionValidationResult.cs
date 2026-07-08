namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Modules;

public sealed record ModuleCompositionValidationResult
{
    public ModuleCompositionValidationResult(
        IReadOnlyList<string> errors,
        string report)
    {
        this.Errors = ModuleMetadataGuards.CopyRequiredList(errors, nameof(errors));
        this.Report = CompositionText.RequireSafeMultilineText(report, nameof(report));
    }

    public bool IsValid => this.Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }
    public string Report { get; }
}
