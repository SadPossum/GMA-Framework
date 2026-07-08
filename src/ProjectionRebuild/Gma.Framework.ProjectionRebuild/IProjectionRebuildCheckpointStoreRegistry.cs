namespace Gma.Framework.ProjectionRebuild;

public interface IProjectionRebuildCheckpointStoreRegistry
{
    IProjectionRebuildCheckpointStore GetRequired(string moduleName);
}
