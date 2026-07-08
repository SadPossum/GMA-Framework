namespace Gma.Framework.Messaging;

public sealed class IntegrationEventSubscriptionRegistry(
    IEnumerable<IntegrationEventSubscription> subscriptions)
    : IIntegrationEventSubscriptionRegistry
{
    public IReadOnlyCollection<IntegrationEventSubscription> Subscriptions { get; } = Validate(subscriptions).ToArray();

    private static IntegrationEventSubscription[] Validate(IEnumerable<IntegrationEventSubscription> subscriptions)
    {
        ArgumentNullException.ThrowIfNull(subscriptions);

        IntegrationEventSubscription[] items = subscriptions
            .Select((subscription, index) => subscription ?? throw new InvalidOperationException(
                $"Integration event subscription at index {index} is null."))
            .ToArray();
        IntegrationEventSubscription? duplicate = items
            .GroupBy(item => $"{item.ConsumerModule}.{item.HandlerName}", StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.First();

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Integration event handler '{duplicate.ConsumerModule}.{duplicate.HandlerName}' is already registered.");
        }

        return items;
    }
}
