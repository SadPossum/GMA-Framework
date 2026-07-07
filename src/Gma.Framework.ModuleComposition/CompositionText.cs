namespace Gma.Framework.ModuleComposition;

internal static class CompositionText
{
    public static string RequireSafeText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string trimmed = value.Trim();
        if (trimmed.Any(char.IsControl))
        {
            throw new ArgumentException($"{parameterName} must not contain control characters.", parameterName);
        }

        return trimmed;
    }

    public static string? OptionalSafeText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return RequireSafeText(value, parameterName);
    }

    public static string RequireSafeMultilineText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string trimmed = value.Trim();
        if (trimmed.Any(character => char.IsControl(character) && character is not '\r' and not '\n'))
        {
            throw new ArgumentException($"{parameterName} must not contain control characters other than line breaks.", parameterName);
        }

        return trimmed;
    }

    public static void EnsureFeatureIdIsNotDefault(CompositionFeatureId id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id.Value))
        {
            throw new ArgumentException($"{parameterName} must be a valid composition feature id.", parameterName);
        }
    }
}
