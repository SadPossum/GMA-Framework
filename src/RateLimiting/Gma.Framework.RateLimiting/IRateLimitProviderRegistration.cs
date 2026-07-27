namespace Gma.Framework.RateLimiting;

public interface IRateLimitProviderRegistration
{
    string ProviderName { get; }
    bool IsDistributed { get; }
}
