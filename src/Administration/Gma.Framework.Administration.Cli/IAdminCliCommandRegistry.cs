namespace Gma.Framework.Administration.Cli;

using System.CommandLine;

public interface IAdminCliCommandRegistry
{
    IServiceProvider Services { get; }
    void AddCommand(string moduleName, Command command);
}
