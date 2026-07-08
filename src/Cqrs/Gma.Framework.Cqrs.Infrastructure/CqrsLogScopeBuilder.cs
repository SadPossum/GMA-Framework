namespace Gma.Framework.Cqrs.Infrastructure;

using System.Diagnostics;
using Gma.Framework.Observability;

internal static class CqrsLogScopeBuilder
{
    public static Dictionary<string, object?> Create(
        CqrsLogScopeContext context,
        IEnumerable<ICqrsLogScopeContributor>? contributors)
    {
        ArgumentNullException.ThrowIfNull(context);

        Dictionary<string, object?> scopeProperties = new()
        {
            [ObservabilityLogPropertyNames.Module] = context.ModuleName,
            [ObservabilityLogPropertyNames.Operation] = context.OperationName,
            [ObservabilityLogPropertyNames.TraceId] = Activity.Current?.TraceId.ToString(),
        };

        if (contributors is null)
        {
            return scopeProperties;
        }

        foreach (ICqrsLogScopeContributor contributor in contributors)
        {
            try
            {
                contributor.Enrich(context, scopeProperties);
            }
            catch (Exception)
            {
                // Scope enrichment is observability-only and must never affect CQRS dispatch.
            }
        }

        return scopeProperties;
    }
}
