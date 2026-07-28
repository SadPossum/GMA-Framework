namespace Gma.Framework.Observability;

public interface ISecuritySignalRecorder
{
    SecuritySignalReceipt Record(
        SecuritySignalDefinition definition,
        Guid? correlationId = null);
}
