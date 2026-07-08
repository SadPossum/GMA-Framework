namespace Gma.Framework.Messaging.Infrastructure;

public enum InboxMessageStatus
{
    Unknown = 0,
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4
}
