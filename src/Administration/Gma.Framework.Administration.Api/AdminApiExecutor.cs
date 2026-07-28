namespace Gma.Framework.Administration.Api;

using Gma.Framework.Naming;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Gma.Framework.Administration;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Framework.Tenancy;
using Gma.Framework.Results;

public sealed class AdminApiExecutor(
    IOptions<AdminApiOptions> options,
    IOptions<TenantOptions> tenantOptions,
    IAdminApiResourceScopeResolver? resourceScopeResolver = null)
{
    private const string AuditHeaderName = "X-Admin-Audit";

    public async Task<IResult> ExecuteAsync<T>(
        HttpContext httpContext,
        AdminOperation operation,
        bool requireTenant,
        Func<CancellationToken, Task<Result<T>>> action,
        CancellationToken cancellationToken,
        string? tenantId = null,
        Func<T, IResult>? onSuccess = null,
        ApiErrorStatusCodeMap? errorStatusCodes = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(action);

        AdminActor? actor = this.ResolveActor(httpContext.User);

        if (actor is null)
        {
            return Results.Problem(
                title: AdminErrors.Unauthorized.Code,
                detail: AdminErrors.Unauthorized.Message,
                statusCode: StatusCodes.Status401Unauthorized);
        }

        IAdminOperationRunner runner = httpContext.RequestServices.GetRequiredService<IAdminOperationRunner>();
        string? effectiveTenantId = this.ResolveTenantId(
            httpContext,
            tenantId,
            requireTenant,
            out Error? tenantError);
        AdminResourceScope? resourceScope = null;
        Error? resourceScopeError =
            resourceScopeResolver is not null &&
            !resourceScopeResolver.TryResolve(httpContext, out resourceScope)
                ? AdminErrors.ResourceScopeInvalid
                : null;
        Error? preAuthorizationError = this.ResolvePreAuthorizationError(
            httpContext.User,
            effectiveTenantId,
            requireTenant,
            httpContext) ?? tenantError ?? resourceScopeError;
        AdminOperationExecutionResult<T> execution = await runner.ExecuteAsync(
            new AdminOperationContext(
                actor,
                operation,
                effectiveTenantId,
                requireTenant,
                preAuthorizationError,
                resourceScope),
            action,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(execution.AuditError))
        {
            httpContext.Response.Headers[AuditHeaderName] = "failed";
        }

        return execution.Status switch
        {
            AdminOperationExecutionStatus.Succeeded => ToSuccessResult(execution.Result.Value, onSuccess),
            AdminOperationExecutionStatus.Unauthorized => ToProblem(execution.Result.Error, StatusCodes.Status403Forbidden),
            AdminOperationExecutionStatus.Failed => ToProblem(execution.Result.Error, GetExpectedFailureStatusCode(execution.Result.Error, errorStatusCodes)),
            AdminOperationExecutionStatus.ValidationFailed => this.ToValidationProblem(
                httpContext,
                execution.Result.Error,
                errorStatusCodes),
            AdminOperationExecutionStatus.UnexpectedFailure => ToProblem(execution.Result.Error, StatusCodes.Status500InternalServerError),
            _ => ToProblem(execution.Result.Error, StatusCodes.Status400BadRequest)
        };
    }

    public Task<IResult> ExecuteAsync(
        HttpContext httpContext,
        AdminOperation operation,
        bool requireTenant,
        Func<CancellationToken, Task<Result>> action,
        CancellationToken cancellationToken,
        string? tenantId = null,
        Func<IResult>? onSuccess = null,
        ApiErrorStatusCodeMap? errorStatusCodes = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        return this.ExecuteAsync(
            httpContext,
            operation,
            requireTenant,
            async token =>
            {
                Result result = await action(token).ConfigureAwait(false);
                return result.IsSuccess
                    ? Result.Success(Unit.Value)
                    : Result.Failure<Unit>(result.Error);
            },
            cancellationToken,
            tenantId,
            _ => onSuccess is null ? Results.NoContent() : onSuccess(),
            errorStatusCodes);
    }

    private AdminActor? ResolveActor(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? actorId =
            user.FindFirstValue(options.Value.ActorIdClaim) ??
            user.FindFirstValue(ApplicationClaimNames.Subject) ??
            user.FindFirstValue(ClaimTypes.NameIdentifier) ??
            user.FindFirstValue(ClaimTypes.Name);

        return AdminActor.TrySystem(actorId, out AdminActor? actor)
            ? actor
            : null;
    }

    private string? ResolveTenantId(
        HttpContext httpContext,
        string? explicitTenantId,
        bool requireTenant,
        out Error? tenantError)
    {
        tenantError = null;

        if (!string.IsNullOrWhiteSpace(explicitTenantId))
        {
            if (TenantIds.TryNormalize(explicitTenantId, out string? normalizedExplicitTenantId))
            {
                return normalizedExplicitTenantId;
            }

            tenantError = AdminErrors.TenantInvalid;
            return null;
        }

        TenantOptions tenancy = tenantOptions.Value;

        if (!requireTenant || !tenancy.Enabled)
        {
            return null;
        }

        if (!httpContext.Request.Headers.TryGetValue(tenancy.HeaderName, out StringValues headerValues))
        {
            return null;
        }

        if (headerValues.Count != 1)
        {
            tenantError = AdminErrors.TenantInvalid;
            return null;
        }

        if (string.IsNullOrWhiteSpace(headerValues[0]))
        {
            return null;
        }

        if (TenantIds.TryNormalize(headerValues[0], out string? normalizedTenantId))
        {
            return normalizedTenantId;
        }

        tenantError = AdminErrors.TenantInvalid;
        return null;
    }

    private Error? ResolvePreAuthorizationError(
        ClaimsPrincipal user,
        string? tenantId,
        bool requireTenant,
        HttpContext httpContext)
    {
        AdminApiOptions adminOptions = options.Value;
        AuthenticationAssuranceRequirement? assurance = adminOptions.AuthenticationAssurance;
        if (assurance is not null)
        {
            TimeProvider timeProvider =
                httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
            if (!AuthenticationAssuranceEvaluator.IsSatisfied(
                    user,
                    assurance,
                    timeProvider.GetUtcNow()))
            {
                return new Error(
                    AuthenticationAssuranceHttpErrorCodes.InsufficientAuthentication,
                    "A stronger or more recent authentication event is required.");
            }
        }

        if (!requireTenant ||
            !adminOptions.RequireTenantClaimMatch ||
            string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(adminOptions.TenantIdClaim))
        {
            return null;
        }

        string? tenantClaim = user.FindFirstValue(adminOptions.TenantIdClaim);

        if (string.IsNullOrWhiteSpace(tenantClaim))
        {
            return null;
        }

        return TenantIds.TryNormalize(tenantClaim, out string? normalizedTenantClaim) &&
               string.Equals(normalizedTenantClaim, tenantId, StringComparison.Ordinal)
            ? null
            : AdminErrors.TenantClaimMismatch;
    }

    private IResult ToValidationProblem(
        HttpContext httpContext,
        Error error,
        ApiErrorStatusCodeMap? errorStatusCodes)
    {
        AuthenticationAssuranceRequirement? assurance = options.Value.AuthenticationAssurance;
        if (assurance is not null &&
            string.Equals(
                error.Code,
                AuthenticationAssuranceHttpErrorCodes.InsufficientAuthentication,
                StringComparison.Ordinal))
        {
            httpContext.Response.Headers.WWWAuthenticate =
                AuthenticationAssuranceChallenge.Create(assurance);
            return ToProblem(error, StatusCodes.Status401Unauthorized);
        }

        return ToProblem(error, GetExpectedFailureStatusCode(error, errorStatusCodes));
    }

    private static IResult ToSuccessResult<T>(T value, Func<T, IResult>? onSuccess)
    {
        if (onSuccess is not null)
        {
            return onSuccess(value) ??
                throw new InvalidOperationException("Admin API success result callback returned a null result.");
        }

        return ToDefaultSuccessResult(value);
    }

    private static IResult ToDefaultSuccessResult<T>(T value) =>
        value is Unit
            ? Results.NoContent()
            : Results.Ok(value);

    private static int GetExpectedFailureStatusCode(Error error, ApiErrorStatusCodeMap? errorStatusCodes) =>
        (errorStatusCodes ?? ApiErrorStatusCodeMap.Empty).GetStatusCode(error);

    private static IResult ToProblem(Error error, int statusCode) =>
        Results.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: statusCode);
}
