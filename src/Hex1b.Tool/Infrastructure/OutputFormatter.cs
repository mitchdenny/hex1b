using System.Text.Json;

namespace Hex1b.Tool.Infrastructure;

/// <summary>
/// Formats output as human-readable text or JSON based on the --json global option.
/// </summary>
internal sealed class OutputFormatter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool JsonMode { get; set; }

    public void WriteJson<T>(T value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, s_jsonOptions));
    }

    public void WriteLine(string message)
    {
        if (!JsonMode)
        {
            Console.WriteLine(message);
        }
    }

    public void WriteError(string message)
    {
        Console.Error.WriteLine(message);
    }

    public void WriteTable(string[] headers, IEnumerable<string[]> rows)
    {
        if (JsonMode)
        {
            return;
        }

        var widths = new int[headers.Length];
        var allRows = rows.ToList();

        for (var i = 0; i < headers.Length; i++)
        {
            widths[i] = headers[i].Length;
        }

        foreach (var row in allRows)
        {
            for (var i = 0; i < Math.Min(row.Length, widths.Length); i++)
            {
                widths[i] = Math.Max(widths[i], row[i].Length);
            }
        }

        var header = string.Join("  ", headers.Select((h, i) => h.PadRight(widths[i])));
        Console.WriteLine(header);

        foreach (var row in allRows)
        {
            var line = string.Join("  ", row.Select((c, i) => i < widths.Length ? c.PadRight(widths[i]) : c));
            Console.WriteLine(line);
        }
    }
}
