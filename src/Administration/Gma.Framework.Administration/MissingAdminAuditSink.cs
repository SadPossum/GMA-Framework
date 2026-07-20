namespace Gma.Framework.Administration;

internal sealed class MissingAdminAuditSink : IAdminAuditSink
{
    public Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken) =>
        Task.FromException(new InvalidOperationException(
            "No admin audit sink is configured. Compose a durable sink or explicitly register NullAdminAuditSink."));
}
