namespace Gma.Framework.Persistence.EntityFrameworkCore;

using Gma.Framework.Scoping;

public sealed class DesignTimeScopeContext : IScopeContext
{
    public bool IsEnabled => false;
    public string? ScopeId => "default";
}
