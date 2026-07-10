namespace Gma.Framework.Scoping;

public interface IScopeContextAccessor : IScopeContext
{
    void SetScope(string scopeId);
    void ClearScope();
}
