namespace Gma.Framework.Administration.Cli;

using System.Text.Json;

public static class AdminCliOutput
{
    public const string Table = "table";
    public const string Json = "json";
    public const string InvalidOutputMessage = "Output format must be 'table' or 'json'.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string NormalizeFormat(string? output)
    {
        if (TryNormalizeFormat(output, out string normalized))
        {
            return normalized;
        }

        throw new ArgumentException(InvalidOutputMessage, nameof(output));
    }

    public static bool TryNormalizeFormat(string? output, out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        string candidate = output.Trim();
        if (string.Equals(candidate, Table, StringComparison.OrdinalIgnoreCase))
        {
            normalized = Table;
            return true;
        }

        if (string.Equals(candidate, Json, StringComparison.OrdinalIgnoreCase))
        {
            normalized = Json;
            return true;
        }

        return false;
    }

    public static void WriteObject<T>(T value, string output)
    {
        if (NormalizeFormat(output) == Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
            return;
        }

        WriteMessage(value?.ToString() ?? string.Empty);
    }

    public static void WriteMessage(string message) => Console.WriteLine(FormatCell(message));

    public static void WriteError(string message) => Console.Error.WriteLine(FormatCell(message));

    public static void WriteErrorInline(string message) => Console.Error.Write(FormatCell(message));

    public static void WriteErrorLine() => Console.Error.WriteLine();

    public static void WriteRows<T>(
        IReadOnlyCollection<T> rows,
        string output,
        IReadOnlyCollection<(string Header, Func<T, string> Value)> columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        if (NormalizeFormat(output) == Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(rows, JsonOptions));
            return;
        }

        ValidateColumns(columns);

        if (rows.Count == 0)
        {
            WriteMessage("No records.");
            return;
        }

        int[] widths = columns
            .Select(column => Math.Max(column.Header.Length, rows.Max(row => FormatCell(column.Value(row)).Length)))
            .ToArray();

        WriteMessage(string.Join("  ", columns.Select((column, index) => column.Header.PadRight(widths[index]))));
        WriteMessage(string.Join("  ", widths.Select(width => new string('-', width))));

        foreach (T row in rows)
        {
            WriteMessage(string.Join(
                "  ",
                columns.Select((column, index) => FormatCell(column.Value(row)).PadRight(widths[index]))));
        }
    }

    private static void ValidateColumns<T>(IReadOnlyCollection<(string Header, Func<T, string> Value)> columns)
    {
        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one table column is required.", nameof(columns));
        }

        foreach ((string header, Func<T, string> value) in columns)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(header);
            ArgumentNullException.ThrowIfNull(value);
        }
    }

    private static string FormatCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return new string(value.Select(character => char.IsControl(character) ? ' ' : character).ToArray());
    }
}
