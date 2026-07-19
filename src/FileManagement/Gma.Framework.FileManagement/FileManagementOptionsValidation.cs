namespace Gma.Framework.FileManagement;

public static class FileManagementOptionsValidation
{
    public const string FailureMessage = FileManagementOptions.SectionName + " configuration is invalid.";

    public static string[] Validate(FileManagementOptions? options)
    {
        if (options is null)
        {
            return [$"{FileManagementOptions.SectionName} options are required."];
        }

        List<string> failures = [];
        if (options.MaximumObjectBytes <= 0)
        {
            failures.Add($"{FileManagementOptions.SectionName}:MaximumObjectBytes must be greater than zero.");
        }

        if (!Enum.IsDefined(options.Provider))
        {
            failures.Add($"{FileManagementOptions.SectionName}:Provider is not supported.");
        }

        if (options.Enabled && options.Provider == FileStorageProvider.Unknown)
        {
            failures.Add($"{FileManagementOptions.SectionName}:Provider is required when file management is enabled.");
        }

        if (options.AllowedContentTypes is null)
        {
            failures.Add($"{FileManagementOptions.SectionName}:AllowedContentTypes is required.");
        }
        else if (options.AllowedContentTypes.Any(contentType => !FileStorageMetadata.IsValidContentType(contentType)))
        {
            failures.Add($"{FileManagementOptions.SectionName}:AllowedContentTypes contains invalid content types.");
        }

        return [.. failures];
    }
}
