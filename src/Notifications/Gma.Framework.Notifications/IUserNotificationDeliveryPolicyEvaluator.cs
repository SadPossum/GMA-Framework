namespace Gma.Framework.Notifications;

public interface IUserNotificationDeliveryPolicyEvaluator
{
    ValueTask<bool> ShouldDeliverAsync(
        UserNotificationMessage message,
        string deliveryTag,
        CancellationToken cancellationToken = default);
}
