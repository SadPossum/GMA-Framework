namespace Gma.Framework.Notifications;

using Gma.Framework.Naming;

public static class NotificationTags
{
    public const int MaxCount = 32;
    public const int MaxLength = 128;
    public const string DeliveryNamespace = "delivery";
    public const string DomainNamespace = "domain";

    public const string Web = "delivery:web";
    public const string Email = "delivery:email";
    public const string Push = "delivery:push";
    public const string Sms = "delivery:sms";

    public static string Normalize(string value, string parameterName = "tag")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string trimmed = value.Trim();
        string[] segments = trimmed.Split(':', StringSplitOptions.None);
        if (segments.Length != 2)
        {
            throw new ArgumentException(
                "Notification tags must use the '<namespace>:<name>' format.",
                parameterName);
        }

        string tagNamespace = SharedNameSegments.NormalizeKebabSegment(
            segments[0],
            "notification tag namespace",
            parameterName);
        string name = SharedNameSegments.NormalizeKebabSegment(
            segments[1],
            "notification tag name",
            parameterName);
        string normalized = $"{tagNamespace}:{name}";

        return normalized.Length <= MaxLength
            ? normalized
            : throw new ArgumentException(
                $"Notification tags must be {MaxLength} characters or fewer.",
                parameterName);
    }

    public static IReadOnlyList<string> Copy(
        IEnumerable<string>? tags,
        string parameterName = "tags",
        bool useWebDefault = true)
    {
        string[] normalized = (tags ?? [])
            .Select(tag => Normalize(tag, parameterName))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0 && useWebDefault)
        {
            return [Web];
        }

        if (normalized.Length > MaxCount)
        {
            throw new ArgumentException(
                $"Notifications can contain at most {MaxCount} distinct tags.",
                parameterName);
        }

        return Array.AsReadOnly(normalized);
    }

    public static bool IsDelivery(string tag) =>
        Normalize(tag).StartsWith($"{DeliveryNamespace}:", StringComparison.Ordinal);

    public static IReadOnlyList<string> GetDeliveryTags(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        return Array.AsReadOnly(tags
            .Select(tag => Normalize(tag))
            .Where(IsDelivery)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());
    }
}
