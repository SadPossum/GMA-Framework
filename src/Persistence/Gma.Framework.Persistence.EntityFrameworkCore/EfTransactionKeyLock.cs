namespace Gma.Framework.Persistence.EntityFrameworkCore;

using System.Buffers.Binary;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Cqrs;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

public static class EfTransactionKeyLock
{
    private const int ResourceMaxLength = 1_024;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(5);

    public static Task AcquireAsync(
        DbContext dbContext,
        string resource,
        CancellationToken cancellationToken = default) =>
        AcquireAsync(
            dbContext,
            resource,
            EfTransactionKeyLockMode.Exclusive,
            DefaultTimeout,
            cancellationToken);

    public static Task AcquireAsync(
        DbContext dbContext,
        string resource,
        EfTransactionKeyLockMode mode,
        CancellationToken cancellationToken = default) =>
        AcquireAsync(
            dbContext,
            resource,
            mode,
            DefaultTimeout,
            cancellationToken);

    public static Task AcquireAsync(
        DbContext dbContext,
        string resource,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        AcquireAsync(
            dbContext,
            resource,
            EfTransactionKeyLockMode.Exclusive,
            timeout,
            cancellationToken);

    public static async Task AcquireAsync(
        DbContext dbContext,
        string resource,
        EfTransactionKeyLockMode mode,
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

        if (mode is not (EfTransactionKeyLockMode.Shared or
            EfTransactionKeyLockMode.Exclusive))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Transaction key lock mode is invalid.");
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

        if (dbContext.Database.IsNpgsql())
        {
            command.CommandTimeout = ToCommandTimeoutSeconds(timeout);
            command.CommandText = mode == EfTransactionKeyLockMode.Shared
                ? "SELECT pg_advisory_xact_lock_shared(@lock_key);"
                : "SELECT pg_advisory_xact_lock(@lock_key);";
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = "lock_key";
            parameter.DbType = DbType.Int64;
            parameter.Value = BinaryPrimitives.ReadInt64BigEndian(resourceHash);
            command.Parameters.Add(parameter);
            try
            {
                await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (PostgresException exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception.SqlState == PostgresErrorCodes.LockNotAvailable)
            {
                throw new TransactionCoordinationException(
                    TransactionCoordinationFailure.TimedOut,
                    exception);
            }
            catch (PostgresException exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception.SqlState == PostgresErrorCodes.QueryCanceled)
            {
                throw new TransactionCoordinationException(
                    TransactionCoordinationFailure.CanceledByProvider,
                    exception);
            }
            catch (PostgresException exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception.SqlState == PostgresErrorCodes.DeadlockDetected)
            {
                throw new TransactionCoordinationException(
                    TransactionCoordinationFailure.DeadlockVictim,
                    exception);
            }
            catch (NpgsqlException exception) when (
                !cancellationToken.IsCancellationRequested &&
                ContainsTimeout(exception))
            {
                throw new TransactionCoordinationException(
                    TransactionCoordinationFailure.TimedOut,
                    exception);
            }

            return;
        }

        if (dbContext.Database.IsSqlServer())
        {
            command.CommandTimeout = checked(ToCommandTimeoutSeconds(timeout) + 5);
            command.CommandText = """
                DECLARE @lock_result int;
                EXEC @lock_result = sys.sp_getapplock
                    @Resource = @lock_resource,
                    @LockMode = @lock_mode,
                    @LockOwner = 'Transaction',
                    @LockTimeout = @lock_timeout;
                SELECT @lock_result;
                """;
            DbParameter resourceParameter = command.CreateParameter();
            resourceParameter.ParameterName = "lock_resource";
            resourceParameter.DbType = DbType.String;
            resourceParameter.Value = $"gma:{Convert.ToHexString(resourceHash)}";
            command.Parameters.Add(resourceParameter);
            DbParameter modeParameter = command.CreateParameter();
            modeParameter.ParameterName = "lock_mode";
            modeParameter.DbType = DbType.String;
            modeParameter.Value = mode == EfTransactionKeyLockMode.Shared
                ? "Shared"
                : "Exclusive";
            command.Parameters.Add(modeParameter);
            DbParameter timeoutParameter = command.CreateParameter();
            timeoutParameter.ParameterName = "lock_timeout";
            timeoutParameter.DbType = DbType.Int32;
            timeoutParameter.Value = checked((int)Math.Ceiling(timeout.TotalMilliseconds));
            command.Parameters.Add(timeoutParameter);
            object? result;
            try
            {
                result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(
                    "Transaction coordination was canceled by the caller.",
                    exception,
                    cancellationToken);
            }
            catch (SqlException exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception.Number == -2)
            {
                throw new TransactionCoordinationException(
                    TransactionCoordinationFailure.TimedOut,
                    exception);
            }
            catch (SqlException exception) when (
                !cancellationToken.IsCancellationRequested &&
                exception.Number == 1205)
            {
                throw new TransactionCoordinationException(
                    TransactionCoordinationFailure.DeadlockVictim,
                    exception);
            }

            int resultCode = result is null or DBNull
                ? int.MinValue
                : Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
            if (resultCode == -2 && cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (resultCode < 0)
            {
                throw CreateSqlServerFailure(resultCode);
            }

            return;
        }

        throw new InvalidOperationException(
            $"Transaction-scoped key locks do not support provider '{dbContext.Database.ProviderName}'.");
    }

    internal static Exception CreateSqlServerFailure(int resultCode) => resultCode switch
    {
        -1 => new TransactionCoordinationException(TransactionCoordinationFailure.TimedOut),
        -2 => new TransactionCoordinationException(TransactionCoordinationFailure.CanceledByProvider),
        -3 => new TransactionCoordinationException(TransactionCoordinationFailure.DeadlockVictim),
        _ => new InvalidOperationException(
            "The transaction-scoped key lock provider rejected the request.")
    };

    private static int ToCommandTimeoutSeconds(TimeSpan timeout) =>
        Math.Max(1, checked((int)Math.Ceiling(timeout.TotalSeconds)));

    private static bool ContainsTimeout(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutException)
            {
                return true;
            }
        }

        return false;
    }
}

public enum EfTransactionKeyLockMode
{
    Shared = 1,
    Exclusive = 2
}
