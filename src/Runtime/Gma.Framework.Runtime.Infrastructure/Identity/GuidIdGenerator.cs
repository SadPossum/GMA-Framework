namespace Gma.Framework.Runtime.Infrastructure.Identity;

using Gma.Framework.Runtime.Identity;

internal sealed class GuidIdGenerator : IIdGenerator
{
    public Guid NewId() => Guid.NewGuid();
}
