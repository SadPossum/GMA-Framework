namespace Gma.Framework.Observability.Infrastructure;

using System.Text;
using Gma.Framework.Naming;

public static class ModuleNameResolver
{
    public static string FromType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        string assemblyName = type.Assembly.GetName().Name ?? "unknown";
        return FromAssemblyName(assemblyName);
    }

    public static string FromAssemblyName(string assemblyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        string[] segments = assemblyName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        string prefix = segments is ["Gma", "Framework", ..]
            ? "Framework"
            : segments is ["Gma", "Modules", var moduleName, ..]
                ? moduleName
                : segments[0];

        return SharedNameSegments.NormalizeKebabSegment(ToKebabCase(prefix), "module name", nameof(assemblyName));
    }

    private static string ToKebabCase(string value)
    {
        StringBuilder builder = new(value.Length);

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsUpper(character))
            {
                if (ShouldInsertHyphen(value, index))
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static bool ShouldInsertHyphen(string value, int index)
    {
        if (index == 0)
        {
            return false;
        }

        char previous = value[index - 1];
        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        return char.IsUpper(previous) &&
               index + 1 < value.Length &&
               char.IsLower(value[index + 1]);
    }
}
