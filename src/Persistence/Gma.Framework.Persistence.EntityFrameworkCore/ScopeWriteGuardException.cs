namespace Gma.Framework.Persistence.EntityFrameworkCore;

public sealed class ScopeWriteGuardException(string message) : InvalidOperationException(message)
{
}
