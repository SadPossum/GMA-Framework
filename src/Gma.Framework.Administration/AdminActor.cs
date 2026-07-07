namespace Gma.Framework.Administration;

using System.Diagnostics.CodeAnalysis;

public sealed record AdminActor
{
    public const int MaxLength = 256;
    public const string InvalidIdMessage =
        "Admin actor id is required, must be 256 characters or fewer, and cannot contain whitespace or control characters.";

    private AdminActor(string id) => this.Id = id;

    public string Id { get; }

    public static AdminActor System(string id) => new(Normalize(id));

    public static bool TrySystem(string? id, [NotNullWhen(true)] out AdminActor? actor)
    {
        actor = null;

        if (!TryNormalize(id, out string? normalized))
        {
            return false;
        }

        actor = new AdminActor(normalized);
        return true;
    }

    private static string Normalize(string id)
    {
        if (TryNormalize(id, out string? normalized))
        {
            return normalized;
        }

        throw new ArgumentException(InvalidIdMessage, nameof(id));
    }

    private static bool TryNormalize(string? id, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;

        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        string candidate = id.Trim();
        if (candidate.Length > MaxLength ||
            candidate.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }
}
