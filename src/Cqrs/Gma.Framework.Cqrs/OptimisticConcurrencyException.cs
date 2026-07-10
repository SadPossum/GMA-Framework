namespace Gma.Framework.Cqrs;

public sealed class OptimisticConcurrencyException(string moduleName, Exception innerException)
    : Exception($"A concurrent update was detected in module '{moduleName}'. Retry using fresh state.", innerException)
{
    public string ModuleName { get; } = moduleName;
}
