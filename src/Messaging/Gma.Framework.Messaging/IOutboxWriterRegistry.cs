namespace Gma.Framework.Messaging;

public interface IOutboxWriterRegistry
{
    IOutboxWriter GetRequired(string moduleName);
}
