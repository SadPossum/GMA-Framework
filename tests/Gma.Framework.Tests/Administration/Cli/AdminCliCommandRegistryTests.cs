namespace Gma.Framework.Tests;

using Microsoft.Extensions.DependencyInjection;
using Gma.Framework.Administration.Cli;
using System.CommandLine;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminCliCommandRegistryTests
{
    [Fact]
    public void Merges_root_command_subcommands_for_same_module()
    {
        RootCommand root = new("admin");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        AdminCliCommandRegistry registry = new(root, provider);
        registry.AddCommand("administration", new Command("admin") { new Command("bootstrap") });

        registry.AddCommand("administration", new Command("admin") { new Command("roles") });

        Command admin = Assert.Single(root.Subcommands);
        Assert.Contains(admin.Subcommands, command => command.Name == "bootstrap");
        Assert.Contains(admin.Subcommands, command => command.Name == "roles");
    }

    [Fact]
    public void Rejects_root_command_reuse_from_another_module()
    {
        RootCommand root = new("admin");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        AdminCliCommandRegistry registry = new(root, provider);
        registry.AddCommand("auth", new Command("auth"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            registry.AddCommand("catalog", new Command("auth")));

        Assert.Contains("already owned by module 'auth'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_duplicate_subcommands_under_same_root()
    {
        RootCommand root = new("admin");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        AdminCliCommandRegistry registry = new(root, provider);
        registry.AddCommand("auth", new Command("auth") { new Command("members") });

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            registry.AddCommand("auth", new Command("auth") { new Command("members") }));

        Assert.Contains("already registered", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(" Auth ", "auth")]
    [InlineData("auth", "Auth")]
    public void Rejects_names_that_require_normalization(string moduleName, string commandName)
    {
        RootCommand root = new("admin");
        using ServiceProvider provider = new ServiceCollection().BuildServiceProvider();
        AdminCliCommandRegistry registry = new(root, provider);

        Assert.ThrowsAny<ArgumentException>(() => registry.AddCommand(moduleName, new Command(commandName)));
    }
}
