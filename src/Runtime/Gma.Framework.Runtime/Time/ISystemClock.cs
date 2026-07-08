namespace Gma.Framework.Runtime.Time;

public interface ISystemClock
{
    DateTimeOffset UtcNow { get; }
}
