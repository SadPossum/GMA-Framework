namespace Gma.Framework.Administration.Api;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public interface IAdminApiModule
{
    string Name { get; }
    void AddServices(IHostApplicationBuilder builder);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
