namespace Gma.Framework.Cqrs.Infrastructure;

public sealed record CqrsLogScopeContext
{
    public CqrsLogScopeContext(
        string moduleName,
        string operationName,
        Type requestType,
        CqrsRequestKind requestKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(requestType);

        this.ModuleName = moduleName;
        this.OperationName = operationName;
        this.RequestType = requestType;
        this.RequestKind = requestKind;
    }

    public string ModuleName { get; }
    public string OperationName { get; }
    public Type RequestType { get; }
    public CqrsRequestKind RequestKind { get; }
}
