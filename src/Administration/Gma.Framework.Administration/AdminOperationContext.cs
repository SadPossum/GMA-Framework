namespace Gma.Framework.Administration;

using Gma.Framework.Results;

public sealed record AdminOperationContext
{
    public AdminOperationContext(
        AdminActor Actor,
        AdminOperation Operation,
        string? TenantId,
        bool RequireTenant,
        Error? PreAuthorizationError = null,
        AdminResourceScope? ResourceScope = null)
    {
        this.Actor = Actor ?? throw new ArgumentNullException(nameof(Actor));
        this.Operation = Operation ?? throw new ArgumentNullException(nameof(Operation));
        this.TenantId = TenantId;
        this.RequireTenant = RequireTenant;
        this.PreAuthorizationError = PreAuthorizationError;
        this.ResourceScope = ResourceScope;
    }

    public AdminActor Actor { get; }
    public AdminOperation Operation { get; }
    public string? TenantId { get; }
    public bool RequireTenant { get; }
    public Error? PreAuthorizationError { get; }
    public AdminResourceScope? ResourceScope { get; }
}
