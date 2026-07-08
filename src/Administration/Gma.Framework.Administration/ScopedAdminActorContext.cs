namespace Gma.Framework.Administration;

public sealed class ScopedAdminActorContext : IAdminActorContextAccessor
{
    public AdminActor? Actor { get; private set; }

    public void SetActor(AdminActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        this.Actor = actor;
    }
}
