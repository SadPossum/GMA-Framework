namespace Gma.Framework.Administration;

public interface IAdminActorContextAccessor : IAdminActorContext
{
    void SetActor(AdminActor actor);
}
