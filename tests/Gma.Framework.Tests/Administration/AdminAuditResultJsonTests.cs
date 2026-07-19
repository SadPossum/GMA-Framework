namespace Gma.Framework.Tests;

using System.Text.Json;
using Gma.Framework.Administration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdminAuditResultJsonTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(AdminAuditResult.Succeeded, "\"succeeded\"")]
    [InlineData(AdminAuditResult.Denied, "\"denied\"")]
    [InlineData(AdminAuditResult.Failed, "\"failed\"")]
    public void Known_results_round_trip_as_lowercase_strings(
        AdminAuditResult result,
        string json)
    {
        Assert.Equal(json, JsonSerializer.Serialize(result, JsonOptions));
        Assert.Equal(result, JsonSerializer.Deserialize<AdminAuditResult>(json, JsonOptions));
    }

    [Fact]
    public void Result_input_is_case_insensitive()
    {
        Assert.Equal(
            AdminAuditResult.Succeeded,
            JsonSerializer.Deserialize<AdminAuditResult>("\"Succeeded\"", JsonOptions));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"unknown\"")]
    [InlineData("\"other\"")]
    public void Invalid_result_input_is_rejected(string json)
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AdminAuditResult>(json, JsonOptions));
    }

    [Fact]
    public void Unknown_result_output_is_rejected()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Serialize(AdminAuditResult.Unknown, JsonOptions));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Serialize((AdminAuditResult)999, JsonOptions));
    }
}
