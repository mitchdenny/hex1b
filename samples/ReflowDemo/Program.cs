using System.Text;
using Hex1b;
using Hex1b.Input;
using Hex1b.Reflow;
using Hex1b.Widgets;

// Simulated terminal dimensions
var initialWidth = 40;
var terminalHeight = 12;
var currentWidth = initialWidth;

// Plenty of content — at width 40 many lines wrap, generating scrollback.
// Numbered prefixes make it easy to track which lines merge/split.
var contentLines = new[]
{
    "[01] The quick brown fox jumps over the lazy dog repeatedly and endlessly",
    "[02] Short line",
    "[03] Pack my box with five dozen liquor jugs then add some more stuff here",
    "[04] How vexingly quick daft zebras jump over fallen logs at dawn today",
    "[05] ABCDEFGHIJ KLMNOPQRST UVWXYZ 0123456789 abcdefghij klmnopqrst uvw",
    "[06] Tiny",
    "[07] Sphinx of black quartz judge my vow of eternal silence please now",
    "[08] The five boxing wizards jump quickly at dawn every single day again",
    "[09] Brief",
    "[10] Lorem ipsum dolor sit amet consectetur adipiscing elit sed do eiusmod",
    "[11] Grumpy wizards make toxic brew for the evil queen and jack",
    "[12] The jay pig fox zebra and my wolves quack exceedingly loud at night",
    "[13] End marker",
};

// Place cursor mid-screen (row 5 of 12) so the Xterm vs Kitty difference is visible.
// Kitty anchors the cursor row; Xterm fills from the bottom.
var cursorScreenRow = 5;
var xtermState = new SimulatedTerminal(contentLines, initialWidth, terminalHeight, cursorScreenRow, XtermReflowStrategy.Instance);
var kittyState = new SimulatedTerminal(contentLines, initialWidth, terminalHeight, cursorScreenRow, KittyReflowStrategy.Instance);

void Resize(int delta)
{
    var newWidth = Math.Clamp(currentWidth + delta, 10, 120);
    if (newWidth == currentWidth) return;
    xtermState.Resize(newWidth);
    kittyState.Resize(newWidth);
    currentWidth = newWidth;
}

// Build the display application
await using var display = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp((app, options) => ctx =>
    {
        return ctx.VStack(v => [
            v.Text($"  Terminal Reflow Demo \u2014 Width: {currentWidth}"),
            v.Text($"  Xterm: scrollback={xtermState.ScrollbackCount} cursor=({xtermState.CursorX},{xtermState.CursorY})   Kitty: scrollback={kittyState.ScrollbackCount} cursor=({kittyState.CursorX},{kittyState.CursorY})"),
            v.Separator(),
            v.HStack(h => [
                BuildPanel(h, "Xterm", xtermState, currentWidth),
                h.Text("  "),
                BuildPanel(h, "Kitty", kittyState, currentWidth),
            ]),
            v.Separator(),
            v.Text("  \u2190 \u2192 \u00b11   Shift+\u2190 \u2192 \u00b15   R Reset   Ctrl+C Quit"),
        ]).WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.RightArrow).Action(_ => Resize(1), "Widen");
            bindings.Key(Hex1bKey.LeftArrow).Action(_ => Resize(-1), "Narrow");
            bindings.Shift().Key(Hex1bKey.RightArrow).Action(_ => Resize(5), "Widen +5");
            bindings.Shift().Key(Hex1bKey.LeftArrow).Action(_ => Resize(-5), "Narrow -5");
            bindings.Key(Hex1bKey.R).Action(_ => Resize(initialWidth - currentWidth), "Reset");
        });
    })
    .WithMouse()
    .Build();

await display.RunAsync();

// --- Helper: Build a bordered panel with cursor marker ---
static Hex1bWidget BuildPanel<T>(WidgetContext<T> ctx, string label, SimulatedTerminal state, int width)
    where T : Hex1bWidget
{
    var headerFill = new string('\u2500', Math.Max(0, width - label.Length - 3));
    var footerFill = new string('\u2500', width);

    return ctx.VStack(v =>
    {
        var widgets = new List<Hex1bWidget>();
        widgets.Add(v.Text($"\u250c\u2500 {label} {headerFill}\u2510"));

        for (int row = 0; row < state.Height; row++)
        {
            var line = state.GetLine(row);
            if (line.Length > width) line = line[..width];
            else if (line.Length < width) line = line.PadRight(width);

            // Show cursor marker on the cursor row
            var marker = row == state.CursorY ? "\u25c0" : " ";
            widgets.Add(v.Text($"\u2502{line}\u2502{marker}"));
        }

        widgets.Add(v.Text($"\u2514{footerFill}\u2518"));
        return widgets.ToArray();
    }).FixedWidth(width + 4);
}

// --- Simulated terminal: builds cell arrays and delegates reflow to strategy ---
class SimulatedTerminal
{
    public TerminalCell[][] ScreenRows { get; private set; }
    public ReflowScrollbackRow[] ScrollbackRows { get; private set; }
    public int CursorX { get; private set; }
    public int CursorY { get; private set; }
    public int Width { get; private set; }
    public int Height { get; }
    public int ScrollbackCount => ScrollbackRows.Length;

    private readonly ITerminalReflowProvider _strategy;

    public SimulatedTerminal(string[] lines, int width, int height, int cursorScreenRow, ITerminalReflowProvider strategy)
    {
        Height = height;
        Width = width;
        _strategy = strategy;
        var (screen, scrollback, cx, cy) = BuildContent(lines, width, height, cursorScreenRow);
        ScreenRows = screen;
        ScrollbackRows = scrollback;
        CursorX = cx;
        CursorY = cy;
    }

    public void Resize(int newWidth)
    {
        if (newWidth == Width) return;
        var context = new ReflowContext(
            ScreenRows, ScrollbackRows,
            Width, Height, newWidth, Height,
            CursorX, CursorY, InAlternateScreen: false);
        var result = _strategy.Reflow(context);
        ScreenRows = result.ScreenRows;
        ScrollbackRows = result.ScrollbackRows;
        CursorX = result.CursorX;
        CursorY = result.CursorY;
        Width = newWidth;
    }

    public string GetLine(int row)
    {
        if (row < 0 || row >= ScreenRows.Length) return "";
        var cells = ScreenRows[row];
        var sb = new StringBuilder(cells.Length);
        foreach (var cell in cells)
            sb.Append(string.IsNullOrEmpty(cell.Character) ? ' ' : cell.Character[0]);
        return sb.ToString();
    }

    private static (TerminalCell[][] screen, ReflowScrollbackRow[] scrollback, int cursorX, int cursorY)
        BuildContent(string[] lines, int width, int height, int cursorScreenRow)
    {
        var allRows = new List<TerminalCell[]>();

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                allRows.Add(MakeRow("", width, softWrap: false));
                continue;
            }

            int pos = 0;
            while (pos < line.Length)
            {
                var chunkLen = Math.Min(line.Length - pos, width);
                var chunk = line.Substring(pos, chunkLen);
                pos += chunkLen;
                allRows.Add(MakeRow(chunk, width, softWrap: pos < line.Length));
            }
        }

        // Split: scrollback is overflow rows above the visible screen
        var screenStart = Math.Max(0, allRows.Count - height);
        var scrollback = allRows.Take(screenStart)
            .Select(r => new ReflowScrollbackRow(r, width)).ToArray();

        var screen = new List<TerminalCell[]>();
        for (int i = screenStart; i < Math.Min(allRows.Count, screenStart + height); i++)
            screen.Add(allRows[i]);
        while (screen.Count < height)
            screen.Add(MakeEmptyRow(width));

        // Place cursor at the requested screen row (clamped to content)
        var contentRowsInScreen = Math.Min(allRows.Count - screenStart, height);
        var cursorY = Math.Clamp(cursorScreenRow, 0, contentRowsInScreen - 1);
        var cursorRow = allRows[screenStart + cursorY];
        var cursorX = 0;
        for (int i = 0; i < width; i++)
        {
            if (!string.IsNullOrEmpty(cursorRow[i].Character) && cursorRow[i].Character != " ")
                cursorX = i + 1;
        }
        cursorX = Math.Min(cursorX, width - 1);

        return (screen.ToArray(), scrollback, cursorX, cursorY);
    }

    private static TerminalCell[] MakeRow(string text, int width, bool softWrap)
    {
        var cells = new TerminalCell[width];
        for (int i = 0; i < width; i++)
        {
            var ch = i < text.Length ? text[i].ToString() : " ";
            var attrs = (i == width - 1 && softWrap) ? CellAttributes.SoftWrap : CellAttributes.None;
            cells[i] = new TerminalCell(ch, null, null, attrs);
        }
        return cells;
    }

    private static TerminalCell[] MakeEmptyRow(int width)
    {
        var cells = new TerminalCell[width];
        for (int i = 0; i < width; i++)
            cells[i] = TerminalCell.Empty;
        return cells;
    }
}
