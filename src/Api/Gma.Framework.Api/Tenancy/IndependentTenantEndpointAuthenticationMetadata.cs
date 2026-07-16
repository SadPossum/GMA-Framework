namespace Gma.Framework.Api.Tenancy;

internal sealed class IndependentTenantEndpointAuthenticationMetadata
{
    private IndependentTenantEndpointAuthenticationMetadata()
    {
    }

    public static IndependentTenantEndpointAuthenticationMetadata Instance { get; } = new();
}
