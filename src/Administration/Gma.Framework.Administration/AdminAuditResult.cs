namespace Gma.Framework.Administration;

[System.Text.Json.Serialization.JsonConverter(typeof(AdminAuditResultJsonConverter))]
public enum AdminAuditResult
{
    Unknown = 0,
    Succeeded = 1,
    Denied = 2,
    Failed = 3,
    Canceled = 4
}
