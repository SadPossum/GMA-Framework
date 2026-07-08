namespace Gma.Framework.FileManagement.LocalStorage;

using Microsoft.Extensions.Options;

internal sealed class LocalFileStorageOptionsValidator : IValidateOptions<LocalFileStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalFileStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.RootPath) ||
            options.RootPath.Length > LocalFileStorageOptions.RootPathMaxLength ||
            options.RootPath.Any(char.IsControl) ||
            options.RootPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{LocalFileStorageOptions.SectionName}:RootPath is required, must be {LocalFileStorageOptions.RootPathMaxLength} characters or fewer, and must be a valid path.");
        }

        return ValidateOptionsResult.Success;
    }
}
