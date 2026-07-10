namespace Gma.Framework.Scoping.Infrastructure;

using Gma.Framework.Naming;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Options;

internal sealed class DefaultScopeContext(IOptions<ScopeOptions> options) : IScopeContextAccessor
{
    private string? scopeId = NormalizeDefaultScopeId(options.Value);

    public bool IsEnabled => options.Value.Enabled;
    public string? ScopeId => this.scopeId;

    public void SetScope(string scopeId) => this.scopeId = ScopeIds.Normalize(scopeId);

    public void ClearScope() => this.scopeId = NormalizeDefaultScopeId(options.Value);

    private static string NormalizeDefaultScopeId(ScopeOptions options) =>
        ScopeIds.Normalize(options.LocalDefaultScopeId);
}
