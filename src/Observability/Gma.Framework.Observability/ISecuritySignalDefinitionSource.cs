namespace Gma.Framework.Observability;

public interface ISecuritySignalDefinitionSource
{
    IReadOnlyCollection<SecuritySignalDefinition> Definitions { get; }
}
