namespace Gma.Framework.Runtime.Infrastructure.Time;

using Gma.Framework.Runtime.Time;

internal sealed class SystemClock : ISystemClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
