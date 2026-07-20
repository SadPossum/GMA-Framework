namespace Gma.Framework.Tasks;

using Gma.Framework.Naming;

public static class TaskNames
{
    public const int ModuleNameMaxLength = 128;
    public const int TaskNameMaxLength = 128;
    public const int WorkerGroupMaxLength = 128;
    public const int ControlCommandMaxLength = 256;
    public const int ActorMaxLength = 256;
    public const int WorkerIdMaxLength = 256;
    public const int DeduplicationKeyMaxLength = 256;

    public static string NormalizeTaskName(string taskName, string parameterName = "taskName") =>
        NormalizeNamedSegment(taskName, "task name", TaskNameMaxLength, parameterName);

    public static string NormalizeWorkerGroup(string workerGroup, string parameterName = "workerGroup") =>
        NormalizeNamedSegment(workerGroup, "worker group", WorkerGroupMaxLength, parameterName);

    public static string NormalizeWorkerId(string workerId, string parameterName = "workerId")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId, parameterName);

        string normalized = workerId.Trim().ToLowerInvariant();
        if (normalized.Length > WorkerIdMaxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"{parameterName} must be {WorkerIdMaxLength} characters or fewer and cannot contain whitespace or control characters.",
                parameterName);
        }

        return normalized;
    }

    public static string? NormalizeOptionalDeduplicationKey(string? deduplicationKey, string parameterName = "deduplicationKey")
    {
        if (string.IsNullOrWhiteSpace(deduplicationKey))
        {
            return null;
        }

        string normalized = deduplicationKey.Trim().ToLowerInvariant();
        if (normalized.Length > DeduplicationKeyMaxLength ||
            normalized.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                $"{parameterName} must be {DeduplicationKeyMaxLength} characters or fewer and cannot contain whitespace or control characters.",
                parameterName);
        }

        return normalized;
    }

    public static string NormalizeModuleName(string moduleName, string parameterName = "moduleName") =>
        NormalizeNamedSegment(moduleName, "module name", ModuleNameMaxLength, parameterName);

    public static string NormalizeScopeId(string scopeId, string parameterName = "scopeId") =>
        ScopeIds.TryNormalize(scopeId, out string? normalized)
            ? normalized
            : throw new ArgumentException(
                $"Scope id is required, must be {ScopeIds.MaxLength} characters or fewer, and cannot contain whitespace or control characters.",
                parameterName);

    public static string NormalizeControlCommandName(string commandName, string parameterName = "commandName")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName, parameterName);

        string normalized = commandName.Trim().ToLowerInvariant();
        if (normalized.Length > ControlCommandMaxLength ||
            normalized.StartsWith('.') ||
            normalized.EndsWith('.') ||
            normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{parameterName} must be a dot-separated command name {ControlCommandMaxLength} characters or fewer.",
                parameterName);
        }

        string[] segments = normalized.Split('.');
        if (segments.Length < 2)
        {
            throw new ArgumentException($"{parameterName} must be a dot-separated command name.", parameterName);
        }

        foreach (string segment in segments)
        {
            _ = SharedNameSegments.NormalizeKebabSegment(segment, "control command segment", parameterName);
        }

        return normalized;
    }

    public static string? NormalizeOptionalActor(string? actor, string parameterName = "actor")
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            return null;
        }

        string normalized = actor.Trim();
        if (normalized.Length > ActorMaxLength ||
            normalized.Any(char.IsControl))
        {
            throw new ArgumentException(
                $"{parameterName} must be {ActorMaxLength} characters or fewer and cannot contain control characters.",
                parameterName);
        }

        return normalized;
    }

    private static string NormalizeNamedSegment(
        string value,
        string description,
        int maxLength,
        string parameterName)
    {
        string normalized = SharedNameSegments.NormalizeKebabSegment(value, description, parameterName);
        return normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException(
                $"{parameterName} must be a lowercase kebab-case {description} {maxLength} characters or fewer.",
                parameterName);
    }
}
