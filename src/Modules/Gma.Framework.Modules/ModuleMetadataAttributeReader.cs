namespace Gma.Framework.Modules;

using System.Reflection;

public static class ModuleMetadataAttributeReader
{
    public static ModuleMetadataItems Read(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        ModuleMetadataItem[] items = targetType
            .GetCustomAttributes(inherit: false)
            .OfType<IModuleMetadataContributor>()
            .Select(attribute => attribute.CreateMetadataItem())
            .ToArray();

        return ModuleMetadataItems.Create(items);
    }

    public static TAttribute GetRequired<TAttribute>(Type targetType, string targetKind)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        return targetType
            .GetCustomAttributes(typeof(TAttribute), inherit: false)
            .OfType<TAttribute>()
            .SingleOrDefault() ?? throw new InvalidOperationException(
            $"{targetKind} '{targetType.FullName}' must declare {typeof(TAttribute).Name} metadata.");
    }

    public static TAttribute? GetOptional<TAttribute>(Type targetType)
        where TAttribute : Attribute
    {
        ArgumentNullException.ThrowIfNull(targetType);

        return targetType
            .GetCustomAttributes(typeof(TAttribute), inherit: false)
            .OfType<TAttribute>()
            .SingleOrDefault();
    }
}
