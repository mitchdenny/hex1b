using System.Text;

/// <summary>
/// One numbered demo screen. Every screen owns the whole terminal: it clears the
/// display, draws a single subject, and then waits before the next screen runs.
/// </summary>
/// <remarks>
/// Screens are numbered so a specific one can be named in review and reproduced
/// directly with <c>--screen &lt;number&gt;</c>.
/// </remarks>
/// <param name="Number">The 1-based screen number, stable across a run.</param>
/// <param name="Title">The screen title shown in the header.</param>
/// <param name="Expected">What a DEC-compatible terminal should display.</param>
/// <param name="Body">The bytes that draw the screen, sent after the header.</param>
/// <param name="Notes">Optional extra lines shown under the header.</param>
internal sealed record DemoScreen(
    int Number,
    string Title,
    string Expected,
    byte[] Body,
    IReadOnlyList<string>? Notes = null)
{
    /// <summary>
    /// Gets the label used in the header and in the headless transcript.
    /// </summary>
    public string Label => $"Screen {Number}: {Title}";
}

/// <summary>
/// Renders a <see cref="DemoScreen"/> as terminal bytes.
/// </summary>
internal static class DemoScreenRenderer
{
    /// <summary>
    /// The row the screen body starts on, leaving the header above it.
    /// </summary>
    public const int BodyRow = 5;

    /// <summary>
    /// Builds the header, positions the cursor at the body row, and appends the
    /// screen body.
    /// </summary>
    /// <remarks>
    /// The mode reset runs before the clear so a screen can never inherit margins,
    /// origin mode, or DECSDM state from the screen before it.
    /// </remarks>
    public static byte[] Render(DemoScreen screen, int total)
    {
        var header = new StringBuilder();
        header.Append(RawCursorScene.ResetSequence);
        header.Append("\x1b[2J\x1b[H");
        header.Append($"\x1b[1m{screen.Label}\x1b[0m  ({screen.Number}/{total})\r\n");
        header.Append($"Expected: {screen.Expected}\r\n");
        if (screen.Notes is { Count: > 0 })
        {
            foreach (var note in screen.Notes)
            {
                header.Append($"{note}\r\n");
            }
        }

        header.Append($"\x1b[{BodyRow};1H");

        return
        [
            .. Encoding.ASCII.GetBytes(header.ToString()),
            .. screen.Body,
        ];
    }

    /// <summary>
    /// Builds the footer prompt shown while a screen waits for input.
    /// </summary>
    public static byte[] RenderPrompt(DemoScreen screen, int total, int promptRow)
    {
        var last = screen.Number == total;
        var action = last ? "quit" : "continue";
        return Encoding.ASCII.GetBytes(
            $"\x1b[{promptRow};1H\x1b[2K\x1b[7m Screen {screen.Number}/{total} \x1b[0m " +
            $"Enter/Space {action}  \u2022  p previous  \u2022  q quit");
    }
}
