namespace Gma.Framework.Observability.Infrastructure;

using Gma.Framework.Observability;

internal sealed class SecuritySignalRegistry
{
    private readonly Dictionary<string, SecuritySignalDefinition> definitions;

    public SecuritySignalRegistry(IEnumerable<ISecuritySignalDefinitionSource> sources)
    {
        Dictionary<string, SecuritySignalDefinition> registered =
            new(StringComparer.Ordinal);
        foreach (ISecuritySignalDefinitionSource source in sources)
        {
            IReadOnlyCollection<SecuritySignalDefinition> definitions =
                source.Definitions ??
                throw new InvalidOperationException(
                    $"Security signal definition source '{source.GetType().FullName}' returned null.");
            foreach (SecuritySignalDefinition definition in definitions)
            {
                ArgumentNullException.ThrowIfNull(definition);
                if (!registered.TryAdd(definition.Code, definition))
                {
                    throw new InvalidOperationException(
                        $"Security signal definition '{definition.Code}' is registered more than once.");
                }
            }
        }

        this.definitions = registered;
    }

    public SecuritySignalDefinition Require(SecuritySignalDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!this.definitions.TryGetValue(definition.Code, out SecuritySignalDefinition? registered))
        {
            throw new InvalidOperationException(
                $"Security signal definition '{definition.Code}' is not registered.");
        }

        if (registered != definition)
        {
            throw new InvalidOperationException(
                $"Security signal definition '{definition.Code}' conflicts with its registered category or severity.");
        }

        return registered;
    }
}
