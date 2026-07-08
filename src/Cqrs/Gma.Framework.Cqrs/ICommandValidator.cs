namespace Gma.Framework.Cqrs;

public interface ICommandValidator<in TCommand>
{
    IEnumerable<string> Validate(TCommand command);
}
