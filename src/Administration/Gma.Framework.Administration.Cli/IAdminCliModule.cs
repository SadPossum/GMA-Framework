namespace Gma.Framework.Administration.Cli;

using Microsoft.Extensions.Hosting;

public interface IAdminCliModule
{
    string Name { get; }
    void AddServices(IHostApplicationBuilder builder);
    void MapCommands(IAdminCliCommandRegistry commands);
}
