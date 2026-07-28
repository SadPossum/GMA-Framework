namespace Gma.Framework.Administration.Cli;

using Gma.Framework.Administration;
using System.CommandLine;

public interface IAdminCliResourceScopeResolver
{
    bool TryResolve(
        ParseResult parseResult,
        out AdminResourceScope? resourceScope);
}
