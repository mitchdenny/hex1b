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
    /// The first row of the description block, counting from 1.
    /// </summary>
    /// <remarks>
    /// The description sits below the image rather than above it. A Sixel graphic
    /// is placed at the cursor, and some scenes deliberately place it at the page
    /// origin, so any text above the image would be painted over. Keeping the
    /// description low also leaves the top of the screen free for the graphic.
    /// </remarks>
    public const int DescriptionRow = 22;

    /// <summary>
    /// Builds the screen: resets modes, clears, homes the cursor, and appends the
    /// screen body.
    /// </summary>
    /// <remarks>
    /// The mode reset runs before the clear so a screen can never inherit margins,
    /// origin mode, or DECSDM state from the screen before it.
    /// </remarks>
    public static byte[] Render(DemoScreen screen, int total)
    {
        var prologue = $"{RawCursorScene.ResetSequence}\x1b[2J\x1b[H";

        return
        [
            .. Encoding.ASCII.GetBytes(prologue),
            .. screen.Body,
        ];
    }

    /// <summary>
    /// Builds the description and the footer prompt shown while a screen waits.
    /// </summary>
    /// <remarks>
    /// This is emitted after the body so the description is never overpainted by
    /// the graphic, whatever geometry or anchor the screen used.
    /// </remarks>
    public static byte[] RenderPrompt(DemoScreen screen, int total, int promptRow, bool isLast)
    {
        var text = new StringBuilder();

        // Margins and origin mode are reset again here: a scene may have set a
        // scrolling region, which would otherwise capture the description.
        text.Append(RawCursorScene.ResetSequence);
        text.Append($"\x1b[{DescriptionRow};1H\x1b[0m");
        text.Append($"\x1b[1mScreen {screen.Number}/{total}: {screen.Title}\x1b[0m\r\n");
        foreach (var line in Lines(screen.Expected))
        {
            text.Append($"\x1b[2K{line}\r\n");
        }

        if (screen.Notes is { Count: > 0 })
        {
            foreach (var note in screen.Notes)
            {
                foreach (var line in Lines(note))
                {
                    text.Append($"\x1b[2K{line}\r\n");
                }
            }
        }

        var action = isLast ? "quit" : "next";
        text.Append($"\x1b[{promptRow};1H\x1b[2K\x1b[7m {screen.Number}/{total} \x1b[0m " +
            $"Enter/Space {action}  \u2022  p previous  \u2022  q quit");

        return Encoding.ASCII.GetBytes(text.ToString());
    }

    /// <summary>
    /// Splits a description into display lines.
    /// </summary>
    /// <remarks>
    /// Descriptions are authored with plain newlines, but a terminal in raw mode
    /// needs an explicit carriage return to return to column 1.
    /// </remarks>
    private static IEnumerable<string> Lines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');
}

