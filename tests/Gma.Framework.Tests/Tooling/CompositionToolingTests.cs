namespace Gma.Framework.Tests.Tooling;

using System.Text.RegularExpressions;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class CompositionToolingTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string[] ProductTokens = ["BunkFy", "StayQuest"];

    [Fact]
    public void Module_scaffolder_supports_product_project_prefixes_and_configurable_hosts()
    {
        string source = ReadTool("new-module.ps1");

        string[] requiredTokens =
        [
            "[string] $ProjectPrefix = ''",
            "$projectName = if",
            "[string] $PublicApiHostProject",
            "[string] $PublicApiHostProgram",
            "[string] $PublicApiHostRegistrationMarker",
            "$moduleUsing = \"using $projectName.Api;\"",
        ];

        Assert.DoesNotContain(requiredTokens, token => !source.Contains(token, StringComparison.Ordinal));
        Assert.DoesNotContain("Join-GmaPath 'src\\Hosts\\Host.Api\\Host.Api.csproj'", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Composition_tools_are_product_neutral_and_listed_in_the_framework_solution()
    {
        string[] tools =
        [
            "add-migration.ps1",
            "check-migrations.ps1",
            "check-source-packages.ps1",
            "check-submodule-heads.ps1",
            "composition-common.ps1",
            "export-source-set.ps1",
            "new-module.ps1",
            "sync-solution.ps1",
        ];
        string solution = File.ReadAllText(Path.Combine(RepositoryRoot, "Gma.Framework.slnx"));
        string[] errors = tools
            .Select(tool => Path.Combine(RepositoryRoot, "eng", tool))
            .SelectMany(path =>
            {
                string source = File.ReadAllText(path);
                string relativePath = Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');
                List<string> toolErrors = [];
                if (!solution.Contains(relativePath, StringComparison.Ordinal))
                {
                    toolErrors.Add($"Gma.Framework.slnx missing {relativePath}");
                }

                foreach (string productToken in ProductTokens)
                {
                    if (source.Contains(productToken, StringComparison.OrdinalIgnoreCase))
                    {
                        toolErrors.Add($"{relativePath} contains product token {productToken}");
                    }
                }

                return toolErrors;
            })
            .ToArray();

        Assert.Empty(errors);
    }

    [Fact]
    public void Migration_and_solution_tools_discover_shape_instead_of_assuming_a_brand()
    {
        string addMigration = ReadTool("add-migration.ps1");
        string syncSolution = ReadTool("sync-solution.ps1");

        Assert.Contains("EndsWith('.Persistence'", addMigration, StringComparison.Ordinal);
        Assert.Contains("$projectPrefix.Persistence.${ProviderName}Migrations", addMigration, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectPrefix = \"Gma.Modules.", addMigration, StringComparison.Ordinal);
        Assert.Contains("System.Xml.Linq.XDocument", syncSolution, StringComparison.Ordinal);
        Assert.Contains("[switch] $Check", syncSolution, StringComparison.Ordinal);
    }

    [Fact]
    public void Framework_scripts_do_not_embed_machine_specific_paths()
    {
        Regex drivePath = new(@"(?i)[A-Z]:\\(?:Users|Projects|Work)\\", RegexOptions.CultureInvariant);
        string[] offenders = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "eng"), "*.ps1")
            .Where(path => drivePath.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Solution_sync_filters_generated_directories_relative_to_the_composition_root()
    {
        string source = ReadTool("sync-solution.ps1");

        Assert.Contains("$relativePath -match '(^|[\\\\/])(\\.tmp|bin|obj)([\\\\/]|$)'", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$project.FullName -match", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Solution_sync_uses_runtime_independent_ordinal_ordering()
    {
        string source = ReadTool("sync-solution.ps1");

        Assert.Contains("$sorted.Sort([System.StringComparer]::Ordinal)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Sort-Object", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Solution_sync_preserves_module_role_folders()
    {
        string source = ReadTool("sync-solution.ps1");

        Assert.Contains("function Get-GmaModuleProjectRole", source, StringComparison.Ordinal);
        Assert.Contains("/src/Modules/$($segments[2])/src/$role/", source, StringComparison.Ordinal);
        Assert.Contains("/gma/modules/$($segments[2])/src/$role/", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Solution_sync_keeps_product_extensions_discoverable()
    {
        string source = ReadTool("sync-solution.ps1");

        Assert.Contains("return '/src/Extensions/'", source, StringComparison.Ordinal);
        Assert.Contains("return '/src/Extensions/tests/'", source, StringComparison.Ordinal);
    }

    private static string ReadTool(string name) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, "eng", name));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Gma.Framework.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the GMA Framework repository root.");
    }
}
