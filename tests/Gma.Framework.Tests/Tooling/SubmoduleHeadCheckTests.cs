namespace Gma.Framework.Tests.Tooling;

using System.Diagnostics;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class SubmoduleHeadCheckTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Redirected_checkout_origin_cannot_authorize_a_different_gitlink(bool relativeUrl)
    {
        using Fixture fixture = new(relativeUrl);
        fixture.RedirectCheckout();

        CommandResult result = fixture.Check();

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("declared", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("component", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Declared_source_can_be_checked_without_a_checkout_origin(bool relativeUrl)
    {
        using Fixture fixture = new(relativeUrl);
        Git(fixture.Checkout, "remote", "remove", "origin");

        CommandResult result = fixture.Check();

        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Fact]
    public void Relative_source_uses_the_composition_branch_tracking_remote()
    {
        using Fixture fixture = new(relativeUrl: true);
        fixture.UseTrackingRemote();

        CommandResult result = fixture.Check();

        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Theory]
    [InlineData("https://example.invalid/team/")]
    [InlineData("git@example.invalid:team/")]
    [InlineData("git@example.invalid:/team/")]
    public void Relative_source_preserves_url_transport_semantics(string remotePrefix)
    {
        using Fixture fixture = new(relativeUrl: true);
        fixture.UseTransportUrl(remotePrefix);

        CommandResult result = fixture.Check();

        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Fact]
    public void Local_composition_without_a_remote_resolves_relative_sources_from_its_directory()
    {
        using Fixture fixture = new(relativeUrl: true);
        fixture.RemoveCompositionOrigin();

        CommandResult result = fixture.Check();

        Assert.True(result.ExitCode == 0, result.Output);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "gma submodule " + Guid.NewGuid().ToString("N"));
        private readonly string declared;
        private readonly string composition;
        private readonly string alternate;

        public Fixture(bool relativeUrl)
        {
            Directory.CreateDirectory(this.root);
            this.declared = Path.Combine(this.root, "declared");
            this.alternate = Path.Combine(this.root, "alternate");
            this.composition = Path.Combine(this.root, "composition");
            Initialize(this.declared);
            File.WriteAllText(Path.Combine(this.declared, "content.txt"), "declared source");
            Git(this.declared, "add", "content.txt");
            Git(this.declared, "commit", "-m", "Declared source");
            Initialize(this.composition);
            Git(this.composition, "remote", "add", "origin", Path.Combine(this.root, "composition.git"));
            this.Checkout = Path.Combine(this.composition, "component");
            Git(this.composition, "clone", "--no-hardlinks", this.declared, this.Checkout);
            Git(this.composition, "config", "--file", ".gitmodules", "submodule.component.path", "component");
            Git(this.composition, "config", "--file", ".gitmodules", "submodule.component.url", relativeUrl ? "../declared" : this.declared);
            Git(this.composition, "config", "--file", ".gitmodules", "submodule.component.branch", "dev");
            Git(this.composition, "add", ".gitmodules", "component");
            Git(this.composition, "commit", "-m", "Record declared component");
        }

        public string Checkout { get; }

        public void UseTrackingRemote()
        {
            Git(this.composition, "remote", "rename", "origin", "upstream");
            Git(this.composition, "remote", "add", "origin", Path.Combine(this.root, "wrong", "composition.git"));
            Git(this.composition, "config", "branch.dev.remote", "upstream");
        }

        public void RemoveCompositionOrigin() => Git(this.composition, "remote", "remove", "origin");

        public void UseTransportUrl(string remotePrefix)
        {
            Git(this.composition, "remote", "set-url", "origin", remotePrefix + "composition.git");
            Git(this.composition, "config", "url." + this.root.Replace('\\', '/') + "/.insteadOf", remotePrefix);
        }

        public void RedirectCheckout()
        {
            Git(this.root, "clone", "--no-hardlinks", this.declared, this.alternate);
            ConfigureIdentity(this.alternate);
            File.WriteAllText(Path.Combine(this.alternate, "content.txt"), "different source");
            Git(this.alternate, "add", "content.txt");
            Git(this.alternate, "commit", "-m", "Different source");
            Git(this.Checkout, "remote", "set-url", "origin", this.alternate);
            Git(this.Checkout, "fetch", "origin");
            Git(this.Checkout, "checkout", "--detach", "origin/dev");
            Git(this.composition, "add", "component");
            Git(this.composition, "commit", "-m", "Record redirected component");
        }

        public CommandResult Check() => Run(this.composition, "pwsh", "-NoProfile", "-File", FindChecker(),
            "-RepositoryRoot", this.composition, "-ExpectedBranch", "dev");

        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(this.root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(this.root, recursive: true);
        }
    }

    private static void Initialize(string path)
    {
        Directory.CreateDirectory(path);
        Git(path, "init", "--initial-branch=dev");
        ConfigureIdentity(path);
    }

    private static void ConfigureIdentity(string path)
    {
        Git(path, "config", "user.name", "Composition test");
        Git(path, "config", "user.email", "composition@example.test");
    }

    private static void Git(string directory, params string[] arguments)
    {
        CommandResult result = Run(directory, "git", arguments);
        Assert.True(result.ExitCode == 0, result.Output);
    }

    private static CommandResult Run(string directory, string executable, params string[] arguments)
    {
        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + executable);
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException(executable + " exceeded the test deadline.");
        }

        return new(process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
    }

    private static string FindChecker()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Gma.Framework.slnx")))
            {
                return Path.Combine(directory.FullName, "eng", "check-submodule-heads.ps1");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the GMA Framework repository root.");
    }

    private sealed record CommandResult(int ExitCode, string Output);
}
