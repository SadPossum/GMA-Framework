namespace Gma.Framework.Security.AspNetCore;

using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

internal sealed class AuthenticationAssuranceEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        Endpoint? endpoint = context.HttpContext.GetEndpoint();
        IReadOnlyList<AuthenticationAssuranceMetadata>? orderedMetadata =
            endpoint?.Metadata.GetOrderedMetadata<AuthenticationAssuranceMetadata>();
        AuthenticationAssuranceRequirement? requirement = orderedMetadata is { Count: > 0 }
            ? orderedMetadata[^1].Requirement
            : null;
        if (requirement is null)
        {
            return await next(context).ConfigureAwait(false);
        }

        ClaimsPrincipal principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true || !SatisfiesContext(principal, requirement))
        {
            return Challenge(context.HttpContext, requirement);
        }

        if (requirement.MaxAuthenticationAge is not null &&
            !HasFreshAuthentication(context.HttpContext, principal, requirement.MaxAuthenticationAge.Value))
        {
            return Challenge(context.HttpContext, requirement);
        }

        return await next(context).ConfigureAwait(false);
    }

    private static bool SatisfiesContext(
        ClaimsPrincipal principal,
        AuthenticationAssuranceRequirement requirement)
    {
        if (requirement.AcceptedContextReferences.Count == 0)
        {
            return true;
        }

        string? contextReference = principal.FindFirstValue(ApplicationClaimNames.AuthenticationContextReference);
        return contextReference is not null &&
            requirement.AcceptedContextReferences.Contains(contextReference, StringComparer.Ordinal);
    }

    private static bool HasFreshAuthentication(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        TimeSpan maximumAge)
    {
        string? value = principal.FindFirstValue(ApplicationClaimNames.AuthenticationTime);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long unixTimeSeconds))
        {
            return false;
        }

        DateTimeOffset authenticatedAtUtc;
        try
        {
            authenticatedAtUtc = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        TimeProvider timeProvider = httpContext.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
        DateTimeOffset nowUtc = timeProvider.GetUtcNow();
        return authenticatedAtUtc <= nowUtc && nowUtc - authenticatedAtUtc <= maximumAge;
    }

    private static IResult Challenge(
        HttpContext httpContext,
        AuthenticationAssuranceRequirement requirement)
    {
        httpContext.Response.Headers.WWWAuthenticate = CreateChallenge(requirement);
        return Results.Problem(
            title: AuthenticationAssuranceHttpErrorCodes.InsufficientAuthentication,
            detail: "A stronger or more recent authentication event is required.",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    internal static string CreateChallenge(AuthenticationAssuranceRequirement requirement)
    {
        List<string> parameters =
        [
            "error=\"insufficient_user_authentication\"",
            "error_description=\"A stronger or more recent authentication event is required.\""
        ];

        if (requirement.AcceptedContextReferences.Count > 0)
        {
            string values = string.Join(' ', requirement.AcceptedContextReferences);
            parameters.Add($"acr_values=\"{EscapeQuotedString(values)}\"");
        }

        if (requirement.MaxAuthenticationAge is not null)
        {
            long seconds = checked((long)Math.Ceiling(requirement.MaxAuthenticationAge.Value.TotalSeconds));
            parameters.Add($"max_age=\"{seconds.ToString(CultureInfo.InvariantCulture)}\"");
        }

        return $"Bearer {string.Join(", ", parameters)}";
    }

    private static string EscapeQuotedString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}
