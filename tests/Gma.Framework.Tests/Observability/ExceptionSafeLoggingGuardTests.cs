namespace Gma.Framework.Tests.Observability;

using System.Text.RegularExpressions;
using Xunit;

[Trait("Category", "Architecture")]
public sealed class ExceptionSafeLoggingGuardTests
{
    private static readonly Regex LogInvocation = new(
        @"\.Log(?:Trace|Debug|Information|Warning|Error|Critical)\([\s\S]*?\);",
        RegexOptions.CultureInvariant);

    private static readonly Regex ExceptionDerivedValue = new(
        @"\b(?:exception|ex)\s*\.\s*(?:Message|StackTrace|ToString\s*\()",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LoggerMessageInvocation = new(
        @"\bLog[A-Za-z0-9_]+\(\s*logger\s*,[\s\S]*?\);",
        RegexOptions.CultureInvariant);

    private static readonly Regex RawExceptionArgument = new(
        @"(?:^|[,(])\s*(?:exception|ex)\s*(?:,|\))",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex SensitivePlaceholder = new(
        @"\{[^}]*(?:Tenant|ScopeId|UserId|ActorId|MessageId|NotificationId|DeliveryId|RunId|WorkerId|NodeId|SubscriptionId|CacheSegments|CacheIdentity|Payload|Body|Token|Email|Reason)(?:[^}]*)\}",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    [Fact]
    public void Framework_logs_use_literal_templates_without_raw_exceptions_or_sensitive_correlators()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "src");
        List<string> offenders = [];

        foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                     .Where(path => !IsGeneratedPath(path)))
        {
            string source = File.ReadAllText(path);
            foreach (Match match in LogInvocation.Matches(source))
            {
                string invocation = match.Value;
                string arguments = invocation[(invocation.IndexOf('(') + 1)..].TrimStart();
                if (!arguments.StartsWith('"'))
                {
                    offenders.Add($"{Relative(repositoryRoot, path)} uses a non-literal first logging argument");
                }

                if (ExceptionDerivedValue.IsMatch(invocation))
                {
                    offenders.Add($"{Relative(repositoryRoot, path)} logs exception-derived text");
                }

                if (SensitivePlaceholder.IsMatch(invocation))
                {
                    offenders.Add($"{Relative(repositoryRoot, path)} logs a sensitive or high-cardinality correlator");
                }
            }

            foreach (Match match in LoggerMessageInvocation.Matches(source))
            {
                if (RawExceptionArgument.IsMatch(match.Value))
                {
                    offenders.Add($"{Relative(repositoryRoot, path)} passes a raw exception to a LoggerMessage delegate");
                }
            }
        }

        Assert.Empty(offenders.Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void Framework_log_scopes_do_not_add_concrete_tenant_or_message_identifiers()
    {
        string repositoryRoot = FindRepositoryRoot();
        string[] forbiddenTokens =
        [
            "ObservabilityLogPropertyNames.TenantId",
            "ObservabilityLogPropertyNames.MessageId",
            "ObservabilityLogPropertyNames.MessageScopeId"
        ];
        string[] offenders = Directory.EnumerateFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.cs",
                SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Where(path => forbiddenTokens.Any(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
            .Select(path => Relative(repositoryRoot, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static bool IsGeneratedPath(string path) =>
        path.Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
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
