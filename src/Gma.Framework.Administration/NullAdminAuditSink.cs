namespace Gma.Framework.Administration;

public sealed class NullAdminAuditSink : IAdminAuditSink
{
    public Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
}
