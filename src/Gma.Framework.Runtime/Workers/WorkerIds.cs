namespace Gma.Framework.Runtime.Workers;

public static class WorkerIds
{
    public const int MaxLength = 256;

    public static string Create(string machineName, Guid workerId)
    {
        if (workerId == Guid.Empty)
        {
            throw new ArgumentException("Worker id must not be empty.", nameof(workerId));
        }

        string normalizedMachineName = NormalizeMachineName(machineName);
        string suffix = workerId.ToString("N");
        int maxMachineNameLength = MaxLength - suffix.Length - 1;

        if (normalizedMachineName.Length > maxMachineNameLength)
        {
            normalizedMachineName = normalizedMachineName[..maxMachineNameLength];
        }

        return $"{normalizedMachineName}:{suffix}";
    }

    public static string Normalize(string workerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerId);

        string normalizedWorkerId = workerId.Trim();

        if (normalizedWorkerId.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("Worker id must not contain whitespace or control characters.", nameof(workerId));
        }

        if (normalizedWorkerId.Length > MaxLength)
        {
            throw new ArgumentException($"Worker id must be {MaxLength} characters or fewer.", nameof(workerId));
        }

        return normalizedWorkerId;
    }

    private static string NormalizeMachineName(string machineName)
    {
        if (string.IsNullOrWhiteSpace(machineName))
        {
            return "unknown";
        }

        char[] characters = machineName.Trim().ToLowerInvariant().ToCharArray();
        for (int index = 0; index < characters.Length; index++)
        {
            if (char.IsWhiteSpace(characters[index]) || char.IsControl(characters[index]))
            {
                characters[index] = '-';
            }
        }

        string normalized = new(characters);
        return string.IsNullOrWhiteSpace(normalized)
            ? "unknown"
            : normalized;
    }
}
