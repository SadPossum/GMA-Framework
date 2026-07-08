namespace Gma.Framework.Cqrs;

public interface ITransactionalCommand<TResponse> : ICommand<TResponse> { }
