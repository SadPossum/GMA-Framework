namespace Gma.Framework.Messaging.Infrastructure;

using Gma.Framework.Messaging;

internal sealed class OutboxWriterRegistry : IOutboxWriterRegistry
{
    private readonly Dictionary<string, IOutboxWriter> writersByModule;

    public OutboxWriterRegistry(IEnumerable<IOutboxWriter> writers)
    {
        ArgumentNullException.ThrowIfNull(writers);

        OutboxWriterRegistration[] registrations = writers
            .Select(CreateRegistration)
            .ToArray();

        IGrouping<string, OutboxWriterRegistration>? duplicate = registrations
            .GroupBy(registration => registration.ModuleName, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{duplicate.Count()} outbox writers are registered for module '{duplicate.Key}'.");
        }

        this.writersByModule = registrations.ToDictionary(
            registration => registration.ModuleName,
            registration => registration.Writer,
            StringComparer.Ordinal);
    }

    public IOutboxWriter GetRequired(string moduleName)
    {
        string normalized = IntegrationEventNaming.NormalizeModuleName(moduleName);

        return this.writersByModule.TryGetValue(normalized, out IOutboxWriter? writer)
            ? writer
            : throw new InvalidOperationException($"No outbox writer is registered for module '{normalized}'.");
    }

    private static OutboxWriterRegistration CreateRegistration(IOutboxWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        try
        {
            return new(
                IntegrationEventNaming.NormalizeModuleName(writer.ModuleName, nameof(IOutboxWriter.ModuleName)),
                writer);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Outbox writer '{writer.GetType().FullName}' has an invalid module name.",
                exception);
        }
    }

    private sealed record OutboxWriterRegistration(string ModuleName, IOutboxWriter Writer);
}
