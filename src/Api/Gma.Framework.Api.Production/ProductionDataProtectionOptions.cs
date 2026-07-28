namespace Gma.Framework.Api.Production;

public sealed class ProductionDataProtectionOptions
{
    public const string SectionName = "DataProtection";
    public const int ApplicationNameMaxLength = 128;
    public const int KeyRingPathMaxLength = 1024;

    public bool RequirePersistentKeys { get; set; }
    public string? KeyRingPath { get; set; }
    public string? ApplicationName { get; set; }
}
