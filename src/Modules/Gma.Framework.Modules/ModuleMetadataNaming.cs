namespace Gma.Framework.Modules;

using Gma.Framework.Naming;

public static class ModuleMetadataNaming
{
    private const int FeatureKeyMaxLength = 256;

    public static string NormalizeModuleName(string moduleName, string parameterName) =>
        SharedModuleNames.Normalize(moduleName, parameterName);

    public static string NormalizeFeatureKey(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > FeatureKeyMaxLength)
        {
            throw new ArgumentException(
                $"{parameterName} must be {FeatureKeyMaxLength} characters or fewer.",
                parameterName);
        }

        string[] segments = normalized.Split('.');
        if (segments.Length < 2)
        {
            throw new ArgumentException(
                $"{parameterName} must use a namespaced '<capability>.<entry>' shape.",
                parameterName);
        }

        foreach (string segment in segments)
        {
            _ = SharedNameSegments.NormalizeKebabSegment(segment, "feature key segment", parameterName);
        }

        return normalized;
    }
}
