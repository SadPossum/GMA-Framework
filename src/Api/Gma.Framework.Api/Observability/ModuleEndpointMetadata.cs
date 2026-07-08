namespace Gma.Framework.Api.Observability;

using Gma.Framework.Naming;

public sealed record ModuleEndpointMetadata
{
    public ModuleEndpointMetadata(string moduleName) =>
        this.ModuleName = SharedModuleNames.Normalize(moduleName);

    public string ModuleName { get; }
}
