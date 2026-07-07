namespace Gma.Framework.ProjectionRebuild;

public interface IProjectionRebuildTransactionBoundaryRegistry
{
    IProjectionRebuildTransactionBoundary? GetOptional(string moduleName);
}
