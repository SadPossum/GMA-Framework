namespace Gma.Framework.Api.Serilog;

using System.Text.RegularExpressions;
using global::Serilog;
using Gma.Framework.Observability;

internal sealed class SafeDiagnosticContext : IDiagnosticContext
{
    private const int MaximumDimensionLength = 64;

    private static readonly Regex SafeDimensionPattern = new(
        "^[A-Za-z0-9._:-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> AllowedContributorProperties = new(StringComparer.Ordinal)
    {
        ObservabilityLogPropertyNames.Module,
        ObservabilityLogPropertyNames.Operation,
        ObservabilityLogPropertyNames.Result,
        ObservabilityLogPropertyNames.ErrorCode,
        ObservabilityLogPropertyNames.TenantScoped,
        ObservabilityLogPropertyNames.MessageScoped,
        ObservabilityLogPropertyNames.Subject,
    };

    private readonly Dictionary<string, object?> properties = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, object?> Properties => this.properties;

    public void Set(string propertyName, object? value, bool destructureObjects = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (!AllowedContributorProperties.Contains(propertyName) || !IsSafeContributorValue(propertyName, value))
        {
            return;
        }

        this.properties[propertyName] = value;
    }

    public void SetException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
    }

    public void SetFrameworkProperty(string propertyName, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        if (IsSafeScalar(value))
        {
            this.properties[propertyName] = value;
        }
    }

    private static bool IsSafeContributorValue(string propertyName, object? value)
    {
        if (propertyName is ObservabilityLogPropertyNames.TenantScoped
            or ObservabilityLogPropertyNames.MessageScoped)
        {
            return value is bool;
        }

        return value is string dimension
               && dimension.Length is > 0 and <= MaximumDimensionLength
               && SafeDimensionPattern.IsMatch(dimension);
    }

    private static bool IsSafeScalar(object? value) =>
        value is null
        or bool
        or byte
        or sbyte
        or short
        or ushort
        or int
        or uint
        or long
        or ulong
        or float
        or double
        or decimal
        or string
        or Guid
        or DateTime
        or DateTimeOffset
        or TimeSpan
        or Enum;
}
