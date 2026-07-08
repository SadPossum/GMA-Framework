namespace Gma.Framework.Tasks;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SupportsTaskControlAttribute : Attribute
{
    public static bool IsDefinedOn(Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        return payloadType.IsDefined(typeof(SupportsTaskControlAttribute), inherit: false);
    }
}
