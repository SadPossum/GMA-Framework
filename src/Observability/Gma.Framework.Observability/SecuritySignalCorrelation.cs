namespace Gma.Framework.Observability;

using System.Diagnostics;

public static class SecuritySignalCorrelation
{
    public static string Create(Guid? correlationId = null)
    {
        if (correlationId.HasValue)
        {
            if (correlationId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Security signal correlation id cannot be empty.",
                    nameof(correlationId));
            }

            return correlationId.Value.ToString("N");
        }

        ActivityTraceId traceId = Activity.Current?.TraceId ?? default;
        return traceId != default
            ? traceId.ToString()
            : Guid.CreateVersion7().ToString("N");
    }
}
