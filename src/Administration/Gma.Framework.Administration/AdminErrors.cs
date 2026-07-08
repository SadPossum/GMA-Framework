namespace Gma.Framework.Administration;

using Gma.Framework.Results;

public static class AdminErrors
{
    public static readonly Error Unauthorized = new("Admin.Unauthorized", "The admin actor is not authorized to perform this operation.");
    public static readonly Error TenantRequired = new("Admin.TenantRequired", "A tenant id is required for this admin operation.");
    public static readonly Error TenantInvalid = new("Admin.TenantInvalid", "The tenant id is not valid.");
    public static readonly Error TenantClaimMismatch = new("Admin.TenantClaimMismatch", "The requested tenant does not match the authenticated admin actor tenant claim.");
    public static readonly Error ConfirmationRequired = new("Admin.ConfirmationRequired", "This admin operation requires explicit confirmation.");
    public static readonly Error BootstrapNotAllowed = new("Admin.BootstrapNotAllowed", "Admin bootstrap is not allowed because assignments already exist.");
    public static readonly Error OperationFailed = new("Admin.OperationFailed", "The admin operation failed unexpectedly.");
}
