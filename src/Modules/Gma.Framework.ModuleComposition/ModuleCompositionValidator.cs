namespace Gma.Framework.ModuleComposition;

using System.Globalization;
using System.Text;

public static class ModuleCompositionValidator
{
    public static ModuleCompositionValidationResult Validate(ModuleCompositionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        SelectedModuleProfile[] selectedProfiles = snapshot.SelectedProfiles
            .OrderBy(selection => selection.Profile.ModuleName, StringComparer.Ordinal)
            .ThenBy(selection => selection.Profile.ProfileName, StringComparer.Ordinal)
            .ThenBy(selection => selection.SelectedBy, StringComparer.Ordinal)
            .ToArray();
        ProvidedCompositionFeature[] providedFeatures = snapshot.ProvidedFeatures
            .Concat(selectedProfiles.SelectMany(selection => selection.Profile.Provides))
            .OrderBy(feature => feature.Id.Value, StringComparer.Ordinal)
            .ThenBy(feature => feature.Provider.Value, StringComparer.Ordinal)
            .ToArray();
        RequiredCompositionFeature[] requiredFeatures = snapshot.RequiredFeatures
            .Concat(selectedProfiles.SelectMany(selection => selection.Profile.Requires))
            .OrderBy(feature => feature.Id.Value, StringComparer.Ordinal)
            .ThenBy(feature => feature.Owner, StringComparer.Ordinal)
            .ToArray();
        RequiredCompositionModule[] requiredModules = snapshot.RequiredModules
            .Concat(selectedProfiles.SelectMany(selection => selection.Profile.RequiredModules))
            .OrderBy(module => module.ModuleName, StringComparer.Ordinal)
            .ThenBy(module => module.Owner, StringComparer.Ordinal)
            .ToArray();

        List<string> errors = [];
        ValidateSelectedProfiles(selectedProfiles, errors);
        ValidateDuplicateProviders(providedFeatures, errors);
        ValidateRequiredFeatures(requiredFeatures, providedFeatures, errors);
        ValidateRequiredModules(requiredModules, selectedProfiles, errors);

        string report = BuildReport(selectedProfiles, providedFeatures, requiredFeatures, requiredModules);
        return new ModuleCompositionValidationResult(errors, report);
    }

    public static void ValidateOrThrow(ModuleCompositionSnapshot snapshot)
    {
        ModuleCompositionValidationResult result = Validate(snapshot);
        if (!result.IsValid)
        {
            throw new ModuleCompositionValidationException(result.Errors, result.Report);
        }
    }

    private static void ValidateSelectedProfiles(
        IReadOnlyList<SelectedModuleProfile> selectedProfiles,
        List<string> errors)
    {
        foreach (IGrouping<string, SelectedModuleProfile> moduleGroup in selectedProfiles.GroupBy(
                     selection => selection.Profile.ModuleName,
                     StringComparer.Ordinal))
        {
            string[] profileNames = moduleGroup
                .Select(selection => selection.Profile.ProfileName)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (profileNames.Length > 1)
            {
                errors.Add(
                    $"Module '{moduleGroup.Key}' has multiple selected profiles: {string.Join(", ", profileNames.Select(Quote))}.");
            }
        }
    }

    private static void ValidateDuplicateProviders(
        IReadOnlyList<ProvidedCompositionFeature> providedFeatures,
        List<string> errors)
    {
        foreach (IGrouping<string, ProvidedCompositionFeature> featureGroup in providedFeatures.GroupBy(
                     feature => feature.Id.Value,
                     StringComparer.Ordinal))
        {
            ProvidedCompositionFeature[] distinctProviders = featureGroup
                .GroupBy(feature => feature.Provider.Value, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(feature => feature.Provider.Value, StringComparer.Ordinal)
                .ToArray();

            if (distinctProviders.Length <= 1 ||
                distinctProviders.All(feature => feature.AllowMultipleProviders))
            {
                continue;
            }

            errors.Add(
                $"Feature '{featureGroup.Key}' is provided by multiple providers ({string.Join(", ", distinctProviders.Select(feature => Quote(feature.Provider.Value)))}) but is exclusive.");
        }
    }

    private static void ValidateRequiredFeatures(
        IReadOnlyList<RequiredCompositionFeature> requiredFeatures,
        IReadOnlyList<ProvidedCompositionFeature> providedFeatures,
        List<string> errors)
    {
        HashSet<string> providedIds = providedFeatures
            .Select(feature => feature.Id.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (RequiredCompositionFeature required in requiredFeatures)
        {
            if (providedIds.Contains(required.Id.Value) || required.Optional)
            {
                continue;
            }

            errors.Add(FormatMissingFeature(required));
        }
    }

    private static void ValidateRequiredModules(
        IReadOnlyList<RequiredCompositionModule> requiredModules,
        IReadOnlyList<SelectedModuleProfile> selectedProfiles,
        List<string> errors)
    {
        HashSet<string> selectedModuleNames = selectedProfiles
            .Select(selection => selection.Profile.ModuleName)
            .ToHashSet(StringComparer.Ordinal);

        foreach (RequiredCompositionModule required in requiredModules)
        {
            if (selectedModuleNames.Contains(required.ModuleName) || required.Optional)
            {
                continue;
            }

            errors.Add(FormatMissingModule(required));
        }
    }

    private static string FormatMissingFeature(RequiredCompositionFeature required)
    {
        string message = $"Composition owner '{required.Owner}' requires feature '{required.Id.Value}', but no composed module, host, or adapter provides it.";
        return string.IsNullOrWhiteSpace(required.Reason)
            ? message
            : message + " " + required.Reason;
    }

    private static string FormatMissingModule(RequiredCompositionModule required)
    {
        string message = $"Composition owner '{required.Owner}' requires module '{required.ModuleName}', but that module profile is not selected.";
        return string.IsNullOrWhiteSpace(required.Reason)
            ? message
            : message + " " + required.Reason;
    }

    private static string BuildReport(
        IReadOnlyList<SelectedModuleProfile> selectedProfiles,
        IReadOnlyList<ProvidedCompositionFeature> providedFeatures,
        IReadOnlyList<RequiredCompositionFeature> requiredFeatures,
        IReadOnlyList<RequiredCompositionModule> requiredModules)
    {
        Dictionary<string, string[]> providersByFeature = providedFeatures
            .GroupBy(feature => feature.Id.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(feature => feature.Provider.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        HashSet<string> selectedModules = selectedProfiles
            .Select(selection => selection.Profile.ModuleName)
            .ToHashSet(StringComparer.Ordinal);

        StringBuilder builder = new();
        builder.AppendLine("Module composition report");
        AppendSelectedProfiles(builder, selectedProfiles);
        AppendProvidedFeatures(builder, providedFeatures);
        AppendRequiredFeatures(builder, requiredFeatures, providersByFeature);
        AppendRequiredModules(builder, requiredModules, selectedModules);
        return builder.ToString().TrimEnd();
    }

    private static void AppendSelectedProfiles(StringBuilder builder, IReadOnlyList<SelectedModuleProfile> selectedProfiles)
    {
        builder.AppendLine("Selected modules:");
        if (selectedProfiles.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        foreach (SelectedModuleProfile selection in selectedProfiles)
        {
            AppendInvariantLine(builder, $"  {selection.Profile.ModuleName} profile={selection.Profile.ProfileName} selected-by={selection.SelectedBy}");
        }
    }

    private static void AppendProvidedFeatures(StringBuilder builder, IReadOnlyList<ProvidedCompositionFeature> providedFeatures)
    {
        builder.AppendLine("Provided features:");
        if (providedFeatures.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        foreach (ProvidedCompositionFeature feature in providedFeatures)
        {
            AppendInvariantLine(builder, $"  {feature.Id.Value} by {feature.Provider}");
        }
    }

    private static void AppendRequiredFeatures(
        StringBuilder builder,
        IReadOnlyList<RequiredCompositionFeature> requiredFeatures,
        Dictionary<string, string[]> providersByFeature)
    {
        builder.AppendLine("Required features:");
        if (requiredFeatures.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        foreach (RequiredCompositionFeature required in requiredFeatures)
        {
            string result = providersByFeature.TryGetValue(required.Id.Value, out string[]? providers)
                ? "satisfied by " + string.Join(", ", providers)
                : required.Optional ? "optional missing" : "missing";
            AppendInvariantLine(builder, $"  {required.Owner} requires {required.Id.Value}: {result}");
        }
    }

    private static void AppendRequiredModules(
        StringBuilder builder,
        IReadOnlyList<RequiredCompositionModule> requiredModules,
        HashSet<string> selectedModules)
    {
        builder.AppendLine("Required modules:");
        if (requiredModules.Count == 0)
        {
            builder.AppendLine("  (none)");
            return;
        }

        foreach (RequiredCompositionModule required in requiredModules)
        {
            string result = selectedModules.Contains(required.ModuleName)
                ? "satisfied"
                : required.Optional ? "optional missing" : "missing";
            AppendInvariantLine(builder, $"  {required.Owner} requires {required.ModuleName}: {result}");
        }
    }

    private static void AppendInvariantLine(StringBuilder builder, FormattableString value) =>
        builder.AppendLine(value.ToString(CultureInfo.InvariantCulture));

    private static string Quote(string value) => "'" + value + "'";
}
