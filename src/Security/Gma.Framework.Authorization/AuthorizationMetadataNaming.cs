namespace Gma.Framework.Authorization;

using Gma.Framework.Naming;

internal static class AuthorizationMetadataNaming
{
    private const int PermissionCodeMaxLength = 256;

    public static string NormalizePermissionCode(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > PermissionCodeMaxLength)
        {
            throw new ArgumentException(
                $"{parameterName} must be {PermissionCodeMaxLength} characters or fewer.",
                parameterName);
        }

        string[] segments = normalized.Split('.');
        if (segments.Length < 2)
        {
            throw new ArgumentException($"{parameterName} must be a dot-separated code.", parameterName);
        }

        foreach (string segment in segments)
        {
            _ = SharedNameSegments.NormalizeKebabSegment(segment, "permission code segment", parameterName);
        }

        return normalized;
    }
}
