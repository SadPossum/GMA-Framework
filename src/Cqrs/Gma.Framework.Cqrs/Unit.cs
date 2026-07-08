namespace Gma.Framework.Cqrs;

public readonly record struct Unit
{
    public static Unit Value => new();
}
