namespace Gma.Framework.ModuleComposition;

using Gma.Framework.Modules;

public sealed class ModuleCompositionValidationException(
    IReadOnlyList<string> errors,
    string report) : InvalidOperationException(CreateMessage(errors))
{
    public IReadOnlyList<string> Errors { get; } = ModuleMetadataGuards.CopyRequiredNonEmptyList(errors, nameof(errors));
    public string Report { get; } = CompositionText.RequireSafeMultilineText(report, nameof(report));

    private static string CreateMessage(IReadOnlyList<string> errors)
    {
        IReadOnlyList<string> copied = ModuleMetadataGuards.CopyRequiredNonEmptyList(errors, nameof(errors));
        return "Module composition is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, copied.Select(error => "- " + error));
    }
}
