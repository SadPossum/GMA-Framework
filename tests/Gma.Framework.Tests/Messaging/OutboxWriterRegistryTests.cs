namespace Gma.Framework.Tests;

using Gma.Framework.Messaging;
using Gma.Framework.Messaging.Infrastructure;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OutboxWriterRegistryTests
{
    [Fact]
    public void Resolves_writer_by_module_name()
    {
        RecordingOutboxWriter auth = new(" Auth ");
        RecordingOutboxWriter catalog = new("catalog");
        OutboxWriterRegistry registry = new([catalog, auth]);

        IOutboxWriter resolved = registry.GetRequired("Auth");

        Assert.Same(auth, resolved);
    }

    [Fact]
    public void Fails_when_writer_is_missing_or_duplicated()
    {
        OutboxWriterRegistry missing = new([new RecordingOutboxWriter("auth")]);
        Assert.Throws<InvalidOperationException>(() => missing.GetRequired("catalog"));

        Assert.Throws<InvalidOperationException>(() => new OutboxWriterRegistry(
            [new RecordingOutboxWriter("auth"), new RecordingOutboxWriter(" Auth ")]));
    }

    [Fact]
    public void Fails_when_writer_declares_invalid_module_name()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new OutboxWriterRegistry([new RecordingOutboxWriter("auth.module")]));

        Assert.Contains("invalid module name", exception.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingOutboxWriter(string moduleName) : IOutboxWriter
    {
        public string ModuleName { get; } = moduleName;

        public Task EnqueueAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent =>
            Task.CompletedTask;
    }
}
