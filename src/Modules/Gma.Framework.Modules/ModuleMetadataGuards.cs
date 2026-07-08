namespace Gma.Framework.Modules;

public static class ModuleMetadataGuards
{
    public static IReadOnlyList<T> CopyRequiredList<T>(IReadOnlyList<T>? items, string parameterName)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(items, parameterName);

        T[] values = items.ToArray();
        if (values.Any(item => item is null))
        {
            throw new ArgumentException($"{parameterName} must not contain null entries.", parameterName);
        }

        return Array.AsReadOnly(values);
    }

    public static IReadOnlyList<T> CopyRequiredNonEmptyList<T>(IReadOnlyList<T>? items, string parameterName)
        where T : class
    {
        IReadOnlyList<T> values = CopyRequiredList(items, parameterName);
        if (values.Count == 0)
        {
            throw new ArgumentException($"{parameterName} must contain at least one entry.", parameterName);
        }

        return values;
    }

    public static IReadOnlyList<T> CopyOptionalList<T>(IReadOnlyList<T>? items)
        where T : class =>
        items is null ? [] : CopyRequiredList(items, nameof(items));

    public static void EnsureUnique<T>(
        IEnumerable<T> items,
        Func<T, string> keySelector,
        string description)
    {
        string? duplicate = items
            .Select(keySelector)
            .GroupBy(key => key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate module metadata {description} '{duplicate}'.");
        }
    }
}
