namespace Gma.Framework.Naming;

public static class SharedModuleNames
{
    public static string Normalize(string moduleName, string parameterName = "moduleName") =>
        SharedNameSegments.NormalizeKebabSegment(moduleName, "module name", parameterName);
}
