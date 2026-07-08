namespace Gma.Framework.Cqrs.Infrastructure;

public interface ICqrsLogScopeContributor
{
    void Enrich(CqrsLogScopeContext context, IDictionary<string, object?> scopeProperties);
}
