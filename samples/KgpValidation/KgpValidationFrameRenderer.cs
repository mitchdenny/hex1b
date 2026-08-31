using System.Text;

namespace KgpValidation;

/// <summary>
/// Renders the common explanatory shell around each raw KGP scenario.
/// </summary>
internal static class KgpValidationFrameRenderer
{
    public static byte[] Render(
        KgpValidationScenario scenario,
        int scenarioIndex,
        int scenarioCount,
        int width,
        int height,
        bool enterAlternateScreen,
        int variant = 0)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentOutOfRangeException.ThrowIfNegative(scenarioIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(scenarioCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (variant < 0 || variant >= scenario.VariantCount)
            throw new ArgumentOutOfRangeException(nameof(variant));

        var writer = new KgpProtocolWriter();
        if (enterAlternateScreen)
            writer.Raw("\x1b[?1049h");

        // Every page is self-contained. This prevents a failed scenario from
        // contaminating the visual result of the next one.
        writer.Kgp("a=d,d=R,x=1,y=4294967295,q=2");
        writer.Kgp("a=d,d=A,q=2");
        writer.Raw("\x1b[r\x1b[0m\x1b[?25l\x1b[2J\x1b[H");

        var usableWidth = Math.Max(1, width - 1);
        WriteBanner(
            writer,
            1,
            $" KGP VALIDATION  {scenarioIndex + 1:00}/{scenarioCount:00}  {scenario.Title}",
            usableWidth);
        writer.TextAt(
            2,
            2,
            $"Area: {scenario.Area}",
            Math.Max(1, usableWidth - 2));
        WriteWrappedField(
            writer,
            3,
            "Expected",
            scenario.GetExpected(variant),
            usableWidth);
        WriteWrappedField(
            writer,
            5,
            "Protocol",
            scenario.Protocol,
            usableWidth);
        writer.TextAt(7, 2, new string('-', Math.Max(1, usableWidth - 2)));

        var layout = new KgpScenarioLayout(width, height);
        if (layout.HasGraphicsRoom)
        {
            scenario.RenderVariant(writer, layout, variant);
        }
        else
        {
            writer.TextAt(
                10,
                3,
                $"Resize to at least {KgpScenarioLayout.MinimumWidth}x{KgpScenarioLayout.MinimumHeight} " +
                "to render this scenario.",
                Math.Max(1, usableWidth - 3));
        }

        writer.Raw("\x1b[r\x1b[0m");
        if (scenario.ActionHint is { } actionHint)
        {
            writer.TextAt(
                Math.Max(1, height - 1),
                2,
                actionHint,
                Math.Max(1, usableWidth - 2));
        }
        WriteBanner(
            writer,
            height,
            " N/Space/Right next  P/Left previous  1-9 jump  Q/Esc/Ctrl+C exit ",
            usableWidth);
        return writer.ToUtf8Bytes();
    }

    public static byte[] Cleanup()
        => Encoding.UTF8.GetBytes(
            "\x1b_Ga=d,d=R,x=1,y=4294967295,q=2\x1b\\" +
            "\x1b_Ga=d,d=A,q=2\x1b\\" +
            "\x1b[r\x1b[0m\x1b[?25h\x1b[2J\x1b[H\x1b[?1049l");

    private static void WriteBanner(
        KgpProtocolWriter writer,
        int row,
        string value,
        int width)
    {
        var content = value.Length >= width
            ? value[..width]
            : value.PadRight(width);
        writer.MoveTo(row, 1);
        writer.Raw("\x1b[7m");
        writer.Raw(content);
        writer.Raw("\x1b[0m");
    }

    private static void WriteWrappedField(
        KgpProtocolWriter writer,
        int firstRow,
        string label,
        string value,
        int width)
    {
        var prefix = $"{label}: ";
        var lineWidth = Math.Max(1, width - 2);
        var lines = Wrap(prefix + value, lineWidth, maximumLines: 2);
        writer.TextAt(firstRow, 2, lines[0], lineWidth);
        if (lines.Count > 1)
            writer.TextAt(firstRow + 1, 2, lines[1], lineWidth);
    }

    private static IReadOnlyList<string> Wrap(
        string value,
        int width,
        int maximumLines)
    {
        var words = value.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lines = new List<string>(maximumLines);
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length > 0 && current.Length + 1 + word.Length > width)
            {
                lines.Add(current.ToString());
                current.Clear();
                if (lines.Count == maximumLines)
                    break;
            }

            if (current.Length > 0)
                current.Append(' ');
            current.Append(word);
        }

        if (lines.Count < maximumLines && current.Length > 0)
            lines.Add(current.ToString());
        if (lines.Count == 0)
            lines.Add(string.Empty);

        var representedLength = lines.Sum(line => line.Length);
        if (representedLength < value.Length - 1 && lines.Count == maximumLines)
        {
            var last = lines[^1];
            lines[^1] = width <= 3
                ? new string('.', width)
                : last.Length <= 3
                ? "..."
                : last[..Math.Min(last.Length, width - 3)] + "...";
        }

        return lines;
    }
}
