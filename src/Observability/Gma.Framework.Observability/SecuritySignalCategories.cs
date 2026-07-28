namespace Gma.Framework.Observability;

public static class SecuritySignalCategories
{
    public const string Authentication = "authentication";
    public const string Authorization = "authorization";
    public const string Administration = "administration";
    public const string Privacy = "privacy";
    public const string Integration = "integration";
    public const string Retention = "retention";
    public const string SupplyChain = "supply-chain";

    public static string ToWireName(SecuritySignalCategory category) =>
        category switch
        {
            SecuritySignalCategory.Authentication => Authentication,
            SecuritySignalCategory.Authorization => Authorization,
            SecuritySignalCategory.Administration => Administration,
            SecuritySignalCategory.Privacy => Privacy,
            SecuritySignalCategory.Integration => Integration,
            SecuritySignalCategory.Retention => Retention,
            SecuritySignalCategory.SupplyChain => SupplyChain,
            _ => throw new ArgumentOutOfRangeException(
                nameof(category),
                category,
                "Security signal category is invalid.")
        };
}
