namespace Gma.Framework.Api.Modules;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public interface IModule
{
    string Name { get; }
    void AddServices(IHostApplicationBuilder builder);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
