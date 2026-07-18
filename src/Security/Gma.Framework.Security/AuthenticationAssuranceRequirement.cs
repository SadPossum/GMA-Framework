namespace Gma.Framework.Security;

using System.Collections.ObjectModel;

public sealed record AuthenticationAssuranceRequirement
{
    public const int ContextReferenceMaxLength = 256;

    private readonly ReadOnlyCollection<string> acceptedContextReferences;

    public AuthenticationAssuranceRequirement(
        IEnumerable<string>? acceptedContextReferences = null,
        TimeSpan? maxAuthenticationAge = null)
    {
        this.acceptedContextReferences = NormalizeContextReferences(acceptedContextReferences);

        if (maxAuthenticationAge is not null && maxAuthenticationAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAuthenticationAge),
                maxAuthenticationAge,
                "Maximum authentication age must be greater than zero.");
        }

        if (this.acceptedContextReferences.Count == 0 && maxAuthenticationAge is null)
        {
            throw new ArgumentException(
                "At least one accepted authentication context or a maximum authentication age is required.",
                nameof(acceptedContextReferences));
        }

        this.MaxAuthenticationAge = maxAuthenticationAge;
    }

    public IReadOnlyList<string> AcceptedContextReferences => this.acceptedContextReferences;
    public TimeSpan? MaxAuthenticationAge { get; }

    private static ReadOnlyCollection<string> NormalizeContextReferences(
        IEnumerable<string>? contextReferences)
    {
        if (contextReferences is null)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }

        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string? contextReference in contextReferences)
        {
            string candidate = contextReference?.Trim() ?? string.Empty;
            if (candidate.Length == 0 ||
                candidate.Length > ContextReferenceMaxLength ||
                candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            {
                throw new ArgumentException(
                    $"Authentication context references are required, must be {ContextReferenceMaxLength} characters or fewer, and cannot contain whitespace or control characters.",
                    nameof(contextReferences));
            }

            if (seen.Add(candidate))
            {
                normalized.Add(candidate);
            }
        }

        return normalized.AsReadOnly();
    }
}
