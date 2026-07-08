namespace Gma.Framework.Tasks;

public sealed class TaskHandlerRegistry : ITaskHandlerRegistry
{
    private readonly Dictionary<string, TaskHandlerRegistration> registrationsByTask;

    public TaskHandlerRegistry(IEnumerable<TaskHandlerRegistration> registrations)
    {
        this.registrationsByTask = Validate(registrations);
        this.Registrations = this.registrationsByTask.Values.ToArray();
    }

    public IReadOnlyCollection<TaskHandlerRegistration> Registrations { get; }

    public TaskHandlerRegistration? Find(string moduleName, string taskName, int payloadVersion = 1)
    {
        string key = CreateKey(
            TaskNames.NormalizeModuleName(moduleName, nameof(moduleName)),
            TaskNames.NormalizeTaskName(taskName, nameof(taskName)),
            NormalizePayloadVersion(payloadVersion));

        return this.registrationsByTask.TryGetValue(key, out TaskHandlerRegistration? registration)
            ? registration
            : null;
    }

    private static Dictionary<string, TaskHandlerRegistration> Validate(IEnumerable<TaskHandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        Dictionary<string, TaskHandlerRegistration> result = new(StringComparer.Ordinal);
        foreach (TaskHandlerRegistration registration in registrations.Select((item, index) => item ?? throw new InvalidOperationException(
                     $"Task handler registration at index {index} is null.")))
        {
            string key = CreateKey(registration.ModuleName, registration.TaskName, registration.PayloadVersion);
            if (result.TryGetValue(key, out TaskHandlerRegistration? existing))
            {
                throw new InvalidOperationException(
                    $"Task handler '{registration.ModuleName}.{registration.TaskName}.v{registration.PayloadVersion}' is already registered by {existing.HandlerType.FullName}.");
            }

            result.Add(key, registration);
        }

        return result;
    }

    private static string CreateKey(string moduleName, string taskName, int payloadVersion) =>
        $"{moduleName}.{taskName}.v{payloadVersion}";

    private static int NormalizePayloadVersion(int payloadVersion) =>
        payloadVersion > 0
            ? payloadVersion
            : throw new ArgumentOutOfRangeException(nameof(payloadVersion), payloadVersion, "Task payload version must be positive.");
}
