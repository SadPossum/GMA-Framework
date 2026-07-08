namespace Gma.Framework.Administration.Cli;

using Gma.Framework.Naming;
using System.CommandLine;

public sealed class AdminCliCommandRegistry(
    RootCommand rootCommand,
    IServiceProvider services) : IAdminCliCommandRegistry
{
    private readonly RootCommand rootCommand = rootCommand ?? throw new ArgumentNullException(nameof(rootCommand));
    private readonly Dictionary<string, string> rootCommandOwners = new(StringComparer.Ordinal);

    public IServiceProvider Services { get; } = services ?? throw new ArgumentNullException(nameof(services));

    public void AddCommand(string moduleName, Command command)
    {
        ArgumentNullException.ThrowIfNull(command);

        string owner = NormalizeOwnerName(moduleName);
        string rootCommandName = NormalizeRootCommandName(command);
        Command? existing = this.rootCommand.Subcommands.SingleOrDefault(item => item.Name == rootCommandName);

        if (existing is null)
        {
            this.rootCommandOwners[rootCommandName] = owner;
            this.rootCommand.Add(command);
            return;
        }

        if (this.rootCommandOwners.TryGetValue(rootCommandName, out string? existingOwner) &&
            !string.Equals(existingOwner, owner, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Admin CLI root command '{rootCommandName}' is already owned by module '{existingOwner}'.");
        }

        this.rootCommandOwners[rootCommandName] = owner;

        foreach (Command subcommand in command.Subcommands)
        {
            if (existing.Subcommands.Any(item => string.Equals(item.Name, subcommand.Name, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Admin CLI command '{rootCommandName} {subcommand.Name}' is already registered.");
            }

            existing.Add(subcommand);
        }
    }

    private static string NormalizeRootCommandName(Command command)
    {
        string normalized = SharedModuleNames.Normalize(command.Name, nameof(Command.Name));

        if (!string.Equals(command.Name, normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Root command names must be lowercase kebab-case and must not require normalization.",
                nameof(command));
        }

        return normalized;
    }

    private static string NormalizeOwnerName(string moduleName)
    {
        string normalized = SharedModuleNames.Normalize(moduleName, nameof(moduleName));

        if (!string.Equals(moduleName, normalized, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Admin CLI module owner names must be lowercase kebab-case and must not require normalization.",
                nameof(moduleName));
        }

        return normalized;
    }
}
