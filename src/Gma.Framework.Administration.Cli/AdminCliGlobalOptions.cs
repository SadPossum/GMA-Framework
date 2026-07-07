namespace Gma.Framework.Administration.Cli;

using System.CommandLine;

public sealed class AdminCliGlobalOptions
{
    public Option<string?> ActorOption { get; } = new("--actor", "-a")
    {
        Description = "Admin actor id. Defaults to the current OS identity.",
        Recursive = true
    };

    public Option<string?> TenantOption { get; } = new("--tenant", "-t")
    {
        Description = "Tenant id for tenant-scoped admin operations.",
        Recursive = true
    };

    public Option<string> OutputOption { get; } = new("--output", "-o")
    {
        Description = "Output format: table or json.",
        DefaultValueFactory = _ => AdminCliOutput.Table,
        Recursive = true
    };
}
