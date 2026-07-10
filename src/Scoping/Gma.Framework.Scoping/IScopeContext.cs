namespace Gma.Framework.Scoping;

public interface IScopeContext
{
    bool IsEnabled { get; }
    string? ScopeId { get; }
}
