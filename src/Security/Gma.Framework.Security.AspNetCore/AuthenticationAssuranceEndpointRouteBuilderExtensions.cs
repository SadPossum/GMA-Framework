namespace Gma.Framework.Security.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

public static class AuthenticationAssuranceEndpointRouteBuilderExtensions
{
    public static RouteHandlerBuilder RequireAuthenticationAssurance(
        this RouteHandlerBuilder builder,
        AuthenticationAssuranceRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(requirement);

        AddMetadata(builder, new AuthenticationAssuranceMetadata(requirement));
        builder.RequireAuthorization();
        return builder.AddEndpointFilter<AuthenticationAssuranceEndpointFilter>();
    }

    public static RouteGroupBuilder RequireAuthenticationAssurance(
        this RouteGroupBuilder builder,
        AuthenticationAssuranceRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(requirement);

        AddMetadata(builder, new AuthenticationAssuranceMetadata(requirement));
        builder.RequireAuthorization();
        return builder.AddEndpointFilter<AuthenticationAssuranceEndpointFilter>();
    }

    private static void AddMetadata(IEndpointConventionBuilder builder, AuthenticationAssuranceMetadata metadata) =>
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(metadata));
}
