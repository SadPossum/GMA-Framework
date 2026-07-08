namespace Gma.Framework.Messaging.Nats;

using Gma.Framework.Messaging;

internal static class NatsConsumerDurableName
{
    public static string Create(
        string durablePrefix,
        string environmentName,
        IntegrationEventSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{NormalizeSegment(durablePrefix, nameof(durablePrefix))}-{NormalizeSegment(environmentName, nameof(environmentName))}-{subscription.ConsumerModule}-{subscription.HandlerName}");
    }

    public static bool IsValidSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized.Length > 0 &&
               normalized[0] != '-' &&
               normalized[^1] != '-' &&
               !normalized.Contains("--", StringComparison.Ordinal) &&
               normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    }

    private static string NormalizeSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        string normalized = value.Trim().ToLowerInvariant();
        if (!IsValidSegment(normalized))
        {
            throw new ArgumentException(
                $"{parameterName} must be a lowercase kebab-case NATS durable-name segment.",
                parameterName);
        }

        return normalized;
    }
}
