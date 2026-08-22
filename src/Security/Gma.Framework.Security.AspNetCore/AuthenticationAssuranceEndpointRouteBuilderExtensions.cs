namespace Gma.Framework.Security.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
        return builder;
    }

    public static RouteGroupBuilder RequireAuthenticationAssurance(
        this RouteGroupBuilder builder,
        AuthenticationAssuranceRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(requirement);

        AddMetadata(builder, new AuthenticationAssuranceMetadata(requirement));
        builder.RequireAuthorization();
        return builder;
    }

    private static void AddMetadata(IEndpointConventionBuilder builder, AuthenticationAssuranceMetadata metadata) =>
        builder.Add(endpointBuilder =>
        {
            endpointBuilder.Metadata.Add(metadata);

            AuthenticationAssuranceRequirement effectiveRequirement =
                AuthenticationAssuranceRequirementComposer.Compose(
                    endpointBuilder.Metadata
                        .OfType<AuthenticationAssuranceMetadata>()
                        .Select(item => item.Requirement)
                        .ToArray());
            EffectiveAuthenticationAssuranceMetadata? existingEffectiveMetadata =
                endpointBuilder.Metadata.OfType<EffectiveAuthenticationAssuranceMetadata>().SingleOrDefault();
            EffectiveAuthenticationAssuranceMetadata effectiveMetadata = new(effectiveRequirement);
            if (existingEffectiveMetadata is null)
            {
                endpointBuilder.Metadata.Add(effectiveMetadata);
            }
            else
            {
                int effectiveMetadataIndex = endpointBuilder.Metadata.IndexOf(existingEffectiveMetadata);
                endpointBuilder.Metadata[effectiveMetadataIndex] = effectiveMetadata;
            }

            if (endpointBuilder.Metadata.Contains(AuthenticationAssuranceFilterRegistrationMetadata.Instance))
            {
                return;
            }

            endpointBuilder.Metadata.Add(AuthenticationAssuranceFilterRegistrationMetadata.Instance);
            endpointBuilder.FilterFactories.Add(static (_, next) =>
            {
                return invocationContext =>
                    ActivatorUtilities
                        .CreateInstance<AuthenticationAssuranceEndpointFilter>(
                            invocationContext.HttpContext.RequestServices)
                        .InvokeAsync(invocationContext, next);
            });
        });
}
