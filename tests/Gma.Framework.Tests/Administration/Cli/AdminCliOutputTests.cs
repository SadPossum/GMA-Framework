namespace Gma.Framework.Tests;

using Gma.Framework.Administration.Cli;
using Xunit;

[Trait("Category", "Unit")]
[Collection(ConsoleTestIsolation.Name)]
public sealed class AdminCliOutputTests
{
    [Theory]
    [InlineData(" table ", AdminCliOutput.Table)]
    [InlineData("JSON", AdminCliOutput.Json)]
    public void Normalize_format_accepts_supported_values(string input, string expected)
    {
        Assert.Equal(expected, AdminCliOutput.NormalizeFormat(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("xml")]
    public void Normalize_format_rejects_unsupported_values(string input)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => AdminCliOutput.NormalizeFormat(input));

        Assert.Contains(AdminCliOutput.InvalidOutputMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_rows_rejects_empty_table_columns()
    {
        Assert.Throws<ArgumentException>(() =>
            AdminCliOutput.WriteRows<Row>(
                [new Row("alpha")],
                AdminCliOutput.Table,
                []));
    }

    [Fact]
    public void Write_message_renders_control_characters_as_safe_single_line_text()
    {
        using StringWriter output = new();
        TextWriter originalOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            AdminCliOutput.WriteMessage("alpha\r\nbeta");
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        string rendered = output.ToString();
        Assert.Contains("alpha  beta", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("alpha\r\nbeta", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_error_methods_use_standard_error()
    {
        using StringWriter error = new();
        TextWriter originalError = Console.Error;
        Console.SetError(error);

        try
        {
            AdminCliOutput.WriteErrorInline("Password: ");
            AdminCliOutput.WriteErrorLine();
            AdminCliOutput.WriteError("failed\r\nnow");
        }
        finally
        {
            Console.SetError(originalError);
        }

        string rendered = error.ToString();
        Assert.Contains("Password:", rendered, StringComparison.Ordinal);
        Assert.Contains("failed  now", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("failed\r\nnow", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_rows_renders_null_and_control_character_cells_as_safe_single_line_text()
    {
        using StringWriter output = new();
        TextWriter originalOutput = Console.Out;
        Console.SetOut(output);

        try
        {
            AdminCliOutput.WriteRows(
                [new Row("alpha\r\nbeta"), new Row(null)],
                AdminCliOutput.Table,
                [("Name", row => row.Name!)]);
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        string rendered = output.ToString();
        Assert.Contains("alpha  beta", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("alpha\r\nbeta", rendered, StringComparison.Ordinal);
    }

    private sealed record Row(string? Name);
}
