namespace Gma.Framework.ProjectionRebuild;

using Gma.Framework.Naming;

public static class ProjectionRebuildNames
{
    public static string NormalizeProjectionName(string projectionName, string parameterName = "projectionName") =>
        SharedNameSegments.NormalizeKebabSegment(projectionName, "projection name", parameterName);
}
