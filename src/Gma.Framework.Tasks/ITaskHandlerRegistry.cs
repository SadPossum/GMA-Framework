namespace Gma.Framework.Tasks;

public interface ITaskHandlerRegistry
{
    IReadOnlyCollection<TaskHandlerRegistration> Registrations { get; }

    TaskHandlerRegistration? Find(string moduleName, string taskName, int payloadVersion = 1);
}
