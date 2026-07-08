namespace Gma.Framework.Cqrs;

public interface IQueryValidator<in TQuery>
{
    IEnumerable<string> Validate(TQuery query);
}
