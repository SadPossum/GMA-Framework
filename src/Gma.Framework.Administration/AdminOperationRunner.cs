namespace Gma.Framework.Administration;

using Gma.Framework.Naming;
using Microsoft.Extensions.Logging;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tenancy;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Results;

internal sealed class AdminOperationRunner(
    IAdminActorContextAccessor actorContext,
    ITenantContextAccessor tenantContext,
    IAdminAuthorizationService authorization,
    IAdminAuditSink auditSink,
    ISystemClock clock,
    IIdGenerator idGenerator,
    ILogger<AdminOperationRunner> logger)
    : IAdminOperationRunner
{
    private const string AuditFailureMessage = "Admin audit failed.";

    public async Task<AdminOperationExecutionResult<T>> ExecuteAsync<T>(
        AdminOperationContext context,
        Func<CancellationToken, Task<Result<T>>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(action);

        actorContext.SetActor(context.Actor);
        tenantContext.ClearTenant();
        string? tenantId = NormalizeTenantId(context.TenantId, out bool tenantIdInvalid);

        if (tenantIdInvalid)
        {
            string? auditError = await this.RecordAuditAsync(
                context,
                tenantId,
                AdminAuditResult.Denied,
                AdminErrors.TenantInvalid.Code,
                cancellationToken).ConfigureAwait(false);

            return new AdminOperationExecutionResult<T>(
                AdminOperationExecutionStatus.ValidationFailed,
                Result.Failure<T>(AdminErrors.TenantInvalid),
                auditError);
        }

        if (context.PreAuthorizationError is not null)
        {
            string? auditError = await this.RecordAuditAsync(
                context,
                tenantId,
                AdminAuditResult.Denied,
                context.PreAuthorizationError.Code,
                cancellationToken).ConfigureAwait(false);

            return new AdminOperationExecutionResult<T>(
                AdminOperationExecutionStatus.ValidationFailed,
                Result.Failure<T>(context.PreAuthorizationError),
                auditError);
        }

        if (tenantContext.IsEnabled && context.RequireTenant && string.IsNullOrWhiteSpace(tenantId))
        {
            string? auditError = await this.RecordAuditAsync(
                context,
                tenantId,
                AdminAuditResult.Denied,
                AdminErrors.TenantRequired.Code,
                cancellationToken).ConfigureAwait(false);

            return new AdminOperationExecutionResult<T>(
                AdminOperationExecutionStatus.ValidationFailed,
                Result.Failure<T>(AdminErrors.TenantRequired),
                auditError);
        }

        if (tenantContext.IsEnabled && !string.IsNullOrWhiteSpace(tenantId))
        {
            tenantContext.SetTenant(tenantId);
        }

        AdminAuthorizationResult authorizationResult;
        try
        {
            authorizationResult = await authorization
                .AuthorizeAsync(context.Actor, context.Operation.Permission, tenantId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await this.FailUnexpectedlyAsync<T>(
                    context,
                    tenantId,
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (!authorizationResult.IsAuthorized)
        {
            string? auditError = await this.RecordAuditAsync(
                context,
                tenantId,
                AdminAuditResult.Denied,
                AdminErrors.Unauthorized.Code,
                cancellationToken).ConfigureAwait(false);

            return new AdminOperationExecutionResult<T>(
                AdminOperationExecutionStatus.Unauthorized,
                Result.Failure<T>(AdminErrors.Unauthorized),
                auditError);
        }

        Result<T> result;
        try
        {
            result = await action(cancellationToken).ConfigureAwait(false) ??
                throw new InvalidOperationException("Admin operation action returned a null result.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await this.FailUnexpectedlyAsync<T>(
                    context,
                    tenantId,
                    exception,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        string? resultAuditError = await this.RecordAuditAsync(
            context,
            tenantId,
            result.IsSuccess ? AdminAuditResult.Succeeded : AdminAuditResult.Failed,
            result.IsSuccess ? null : result.Error.Code,
            cancellationToken).ConfigureAwait(false);

        return new AdminOperationExecutionResult<T>(
            result.IsSuccess ? AdminOperationExecutionStatus.Succeeded : AdminOperationExecutionStatus.Failed,
            result,
            resultAuditError);
    }

    private async Task<AdminOperationExecutionResult<T>> FailUnexpectedlyAsync<T>(
        AdminOperationContext context,
        string? tenantId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        this.LogUnexpectedFailure(context, exception);

        string? auditError = await this.RecordAuditAsync(
            context,
            tenantId,
            AdminAuditResult.Failed,
            AdminErrors.OperationFailed.Code,
            cancellationToken).ConfigureAwait(false);

        return new AdminOperationExecutionResult<T>(
            AdminOperationExecutionStatus.UnexpectedFailure,
            Result.Failure<T>(AdminErrors.OperationFailed),
            auditError);
    }

    private static string? NormalizeTenantId(string? tenantId, out bool invalid)
    {
        invalid = false;

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        if (TenantIds.TryNormalize(tenantId, out string? normalized))
        {
            return normalized;
        }

        invalid = true;
        return null;
    }

    private async Task<string?> RecordAuditAsync(
        AdminOperationContext context,
        string? tenantId,
        AdminAuditResult result,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await auditSink.RecordAsync(
                new AdminAuditRecord(
                    idGenerator.NewId(),
                    context.Actor.Id,
                    tenantId,
                    context.Operation.Name,
                    context.Operation.Permission.Code,
                    result,
                    errorCode,
                    clock.UtcNow),
                cancellationToken).ConfigureAwait(false);

            return null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            this.LogAuditFailure(context, exception);
            return AuditFailureMessage;
        }
    }

    private void LogUnexpectedFailure(AdminOperationContext context, Exception exception)
    {
        try
        {
            logger.LogError(
                exception,
                "Admin operation {OperationName} failed unexpectedly for actor {ActorId}",
                context.Operation.Name,
                context.Actor.Id);
        }
        catch (Exception)
        {
            // Admin operations must still return shaped failures when an observability provider is unavailable.
        }
    }

    private void LogAuditFailure(AdminOperationContext context, Exception exception)
    {
        try
        {
            logger.LogError(
                exception,
                "Admin audit failed for operation {OperationName} and actor {ActorId}",
                context.Operation.Name,
                context.Actor.Id);
        }
        catch (Exception)
        {
            // Audit failures are surfaced through AuditError; logger failures should not hide that result.
        }
    }
}
