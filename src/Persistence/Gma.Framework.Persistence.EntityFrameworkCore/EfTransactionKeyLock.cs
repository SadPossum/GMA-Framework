namespace Gma.Framework.Persistence.EntityFrameworkCore;

using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

public static class EfTransactionKeyLock
{
    private const int ResourceMaxLength = 1_024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(5);

    public static Task AcquireAsync(
        DbContext dbContext,
        string resource,
        CancellationToken cancellationToken = default) =>
        AcquireAsync(dbContext, resource, DefaultTimeout, cancellationToken);

    public static async Task AcquireAsync(
        DbContext dbContext,
        string resource,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(resource);
        if (resource.Length > ResourceMaxLength)
        {
            throw new ArgumentException("Transaction lock resource is invalid.", nameof(resource));
        }

        if (timeout <= TimeSpan.Zero || timeout > MaximumTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Transaction lock timeout must be positive and at most five minutes.");
        }

        IDbContextTransaction? transaction = dbContext.Database.CurrentTransaction;
        if (transaction is null)
        {
            throw new InvalidOperationException("A transaction-scoped key lock requires an active database transaction.");
        }

        byte[] resourceHash = SHA256.HashData(Encoding.UTF8.GetBytes(resource));
        DbConnection connection = dbContext.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(timeout.TotalSeconds));

        if (dbContext.Database.IsNpgsql())
        {
            command.CommandText = "SELECT pg_advisory_xact_lock(@lock_key);";
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = "lock_key";
            parameter.DbType = DbType.Int64;
            parameter.Value = BinaryPrimitives.ReadInt64BigEndian(resourceHash);
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (dbContext.Database.IsSqlServer())
        {
            command.CommandText = """
                DECLARE @lock_result int;
                EXEC @lock_result = sys.sp_getapplock
                    @Resource = @lock_resource,
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = @lock_timeout;
                SELECT @lock_result;
                """;
            DbParameter resourceParameter = command.CreateParameter();
            resourceParameter.ParameterName = "lock_resource";
            resourceParameter.DbType = DbType.String;
            resourceParameter.Value = $"gma:{Convert.ToHexString(resourceHash)}";
            command.Parameters.Add(resourceParameter);
            DbParameter timeoutParameter = command.CreateParameter();
            timeoutParameter.ParameterName = "lock_timeout";
            timeoutParameter.DbType = DbType.Int32;
            timeoutParameter.Value = checked((int)Math.Ceiling(timeout.TotalMilliseconds));
            command.Parameters.Add(timeoutParameter);
            object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            int resultCode = result is null or DBNull
                ? int.MinValue
                : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
            if (resultCode < 0)
            {
                throw new InvalidOperationException($"Could not acquire the transaction-scoped key lock (provider result {resultCode}).");
            }

            return;
        }

        throw new InvalidOperationException(
            $"Transaction-scoped key locks do not support provider '{dbContext.Database.ProviderName}'.");
    }
}
