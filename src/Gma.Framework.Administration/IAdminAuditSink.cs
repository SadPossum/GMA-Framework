namespace Gma.Framework.Administration;

public interface IAdminAuditSink
{
    Task RecordAsync(AdminAuditRecord record, CancellationToken cancellationToken);
}
