namespace Gma.Framework.Api.Observability;

using Microsoft.AspNetCore.Builder;

public static class ModuleEndpointConventionBuilderExtensions
{
    public static TBuilder WithModuleName<TBuilder>(this TBuilder builder, string moduleName)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new ModuleEndpointMetadata(moduleName)));
        return builder;
    }
}
