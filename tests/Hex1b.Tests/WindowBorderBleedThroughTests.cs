using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Reproduction test for the window border background color bleed-through bug.
/// </summary>
/// <remarks>
/// <para><strong>Bug description:</strong></para>
/// <para>
/// When two windows overlap in a <see cref="WindowPanelWidget"/>, the border characters
/// (┌┐└┘─│) of the top window only set a foreground color but do NOT set a background color.
/// This means that if the top window's border overlaps the bottom window's content area,
/// the bottom window's content background color "bleeds through" into the top window's
/// border cells, creating a visual artifact.
/// </para>
///
/// <para><strong>Visual example of the bug:</strong></para>
/// <code>
///     ┌──── Window B (red background content) ────┐
///     │ Title bar B                                │
///     │ ██ Red content area here ██████████████████ │
///     │ ████████┌──── Window A (on top) ────┐█████ │
///     │ ████████│ Title bar A               │█████ │
///     │ ████████│ Content A                 │█████ │
///     │ ████████│                           │█████ │
///     │ ████████│                           │█████ │
///     └────────┘│                           │
///               │                           │
///               └───────────────────────────┘
///
///     In the overlap region, Window A's left border character '│' and
///     top-left corner '┌' will have Window B's RED background because
///     WindowNode.Render() only sets foreground color on border characters.
/// </code>
///
/// <para><strong>Root cause (in WindowNode.cs):</strong></para>
/// <para>
/// The content area rows correctly set background color:
/// <c>$"{contentBgCode}{new string(' ', innerWidth)}{resetToGlobal}"</c>
/// </para>
/// <para>
/// But border characters only set foreground:
/// <c>$"{borderFg}{vertical}{resetToGlobal}"</c> — no background color code.
/// </para>
/// <para>
/// The same issue affects horizontal borders in <c>RenderHorizontalEdge()</c>.
/// </para>
/// </remarks>
public class WindowBorderBleedThroughTests
{
    /// <summary>
    /// The distinctive background color used for Window B's content area.
    /// Bright red (255, 0, 0) is chosen because it's unmistakable in assertions
    /// and easy to spot in diagnostic output.
    /// </summary>
    private static readonly Hex1bColor WindowBContentBackground = Hex1bColor.FromRgb(255, 0, 0);

    /// <summary>
    /// Reproduces the border bleed-through bug with two overlapping windows.
    /// </summary>
    /// <remarks>
    /// <para><strong>Test layout (80×24 terminal):</strong></para>
    /// <code>
    ///   Col: 0         10        20        30        40        50
    ///        |         |         |         |         |         |
    ///   Row 0: (empty - WindowPanel fills entire 80x24)
    ///   Row 1: ┌─── Window B ──────────────────────┐
    ///   Row 2: │ Window B (title bar)               │
    ///   Row 3: │ ██ red bg content starts here ████ │   ← Window B content rows 3..10
    ///   Row 4: │ ████████┌─── Window A ──────────┐ │   ← OVERLAP STARTS HERE
    ///   Row 5: │ ████████│ Window A (title bar)   │ │
    ///   Row 6: │ ████████│ Content A              │ │
    ///   Row 7: │ ████████│                        │ │
    ///   Row 8: │ ████████│                        │ │
    ///   Row 9: │ ████████│                        │ │
    ///   Row 10:│ ████████│                        │ │
    ///   Row 11:└─────────│                        │──┘
    ///   Row 12:          │                        │
    ///   Row 13:          └────────────────────────┘
    ///
    ///   Window B: TopLeft position, offset (2, 1), size 40×11
    ///     - Border: row 1 (top), row 11 (bottom), col 2 (left), col 41 (right)
    ///     - Title bar: row 2
    ///     - Content area: rows 3..10, cols 3..40  (red background)
    ///
    ///   Window A: TopLeft position, offset (12, 4), size 30×10
    ///     - Border: row 4 (top), row 13 (bottom), col 12 (left), col 41 (right)
    ///     - Title bar: row 5
    ///     - Content area: rows 6..12, cols 13..40
    ///
    ///   Overlap region (where Window A's borders sit on Window B's red content):
    ///     - Window A top border (row 4, cols 12..40): overlaps Window B content rows 3..10
    ///     - Window A left border (col 12, rows 4..10): overlaps Window B content cols 3..40
    ///     - Window A title bar left border (col 12, row 5): overlaps Window B content
    /// </code>
    ///
    /// <para><strong>What we assert:</strong></para>
    /// <list type="bullet">
    ///   <item>Sanity check: at least one uncovered Window B content cell IS red (confirms setup is correct).</item>
    ///   <item>Window A's border cells in the overlap region must NOT have the red background from Window B.</item>
    ///   <item>We check a non-zero number of overlap cells (non-vacuous assertion).</item>
    /// </list>
    /// </remarks>
    [Fact]
    public async Task WindowBorder_OverlappingContentWithBackground_BorderBackgroundDoesNotBleed()
    {
        // ── Arrange ──────────────────────────────────────────────────────────

        const int terminalWidth = 80;
        const int terminalHeight = 24;

        // The MenuBar occupies row 0, so the WindowPanel starts at row 1.
        const int panelStartRow = 1;

        // Window position OFFSETS within the panel (passed to WindowPositionSpec).
        // These are relative to the panel's top-left corner.
        const int windowBOffsetX = 2;
        const int windowBOffsetY = 0;  // Top of panel
        const int windowBWidth = 40;
        const int windowBHeight = 11;

        const int windowAOffsetX = 12;
        const int windowAOffsetY = 3;  // Overlaps Window B's content area
        const int windowAWidth = 30;
        const int windowAHeight = 10;

        // Derived: ABSOLUTE screen positions of the windows.
        // WindowPositionSpec(TopLeft, offsetX, offsetY) calculates:
        //   x = panelBounds.X + offsetX,  y = panelBounds.Y + offsetY
        const int windowBAbsX = windowBOffsetX;                    // col 2
        const int windowBAbsY = panelStartRow + windowBOffsetY;    // row 1
        const int windowAAbsX = windowAOffsetX;                    // col 12
        const int windowAAbsY = panelStartRow + windowAOffsetY;    // row 4

        // Derived: Window B's content area on screen (inside borders, below title bar).
        // Border is 1 char on each side. Title bar is 1 row below top border.
        // Content starts at (absX+1, absY+2) and ends at (absX+width-2, absY+height-2).
        const int wbContentLeft = windowBAbsX + 1;                       // col 3
        const int wbContentRight = windowBAbsX + windowBWidth - 2;       // col 40
        const int wbContentTop = windowBAbsY + 2;                        // row 3
        const int wbContentBottom = windowBAbsY + windowBHeight - 2;     // row 10

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(terminalWidth, terminalHeight)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    // MenuBar with a single "Open" action that creates both windows.
                    // We use a MenuBar because it's the standard test pattern for
                    // programmatically opening windows from an event handler.
                    outer.MenuBar(m => [
                        m.Menu("Setup", menu => [
                            menu.MenuItem("Open Windows").OnActivated(e =>
                            {
                                // Window B (opened first = lower z-index = bottom):
                                // Red background with text content.
                                var windowB = e.Windows.Window(w =>
                                    new BackgroundPanelWidget(WindowBContentBackground,
                                        new VStackWidget([
                                            new TextBlockWidget("Window B line 1"),
                                            new TextBlockWidget("Window B line 2"),
                                            new TextBlockWidget("Window B line 3"),
                                            new TextBlockWidget("Window B line 4"),
                                            new TextBlockWidget("Window B line 5"),
                                        ])))
                                    .Title("Window B")
                                    .Size(windowBWidth, windowBHeight)
                                    .Position(new WindowPositionSpec(WindowPosition.TopLeft, windowBOffsetX, windowBOffsetY));
                                e.Windows.Open(windowB);

                                // Window A (opened second = higher z-index = on top):
                                // Plain content, no custom background.
                                var windowA = e.Windows.Window(w =>
                                    new VStackWidget([
                                        new TextBlockWidget("Window A content"),
                                    ]))
                                    .Title("Window A")
                                    .Size(windowAWidth, windowAHeight)
                                    .Position(new WindowPositionSpec(WindowPosition.TopLeft, windowAOffsetX, windowAOffsetY));
                                e.Windows.Open(windowA);
                            })
                        ])
                    ]),
                    // WindowPanel fills the remaining space (row 1..23)
                    outer.WindowPanel()
                        .Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        // ── Act ──────────────────────────────────────────────────────────────

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Step 1: Wait for the menu bar to render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Setup"), TimeSpan.FromSeconds(5), "menu rendered")
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Step 2: Open the Setup menu via ALT+S, then activate "Open Windows"
        await new Hex1bTerminalInputSequenceBuilder()
            .Alt().Key(Hex1bKey.S)
            .WaitUntil(s => s.ContainsText("Open Windows"), TimeSpan.FromSeconds(5), "menu opened")
            .Key(Hex1bKey.Enter)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        // Step 3: Wait for both windows to render, then capture a snapshot.
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Window A content") && s.ContainsText("Window B line 1"),
                TimeSpan.FromSeconds(5), "both windows rendered")
            .Wait(TimeSpan.FromMilliseconds(200))  // Allow final render pass to complete
            .Capture("window-border-bleed")
            .Ctrl().Key(Hex1bKey.C)  // Exit the app
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // ── Assert ───────────────────────────────────────────────────────────

        // Sanity check 1: Verify that an uncovered Window B content cell actually has the red
        // background. This confirms our test setup is correct and BackgroundPanelWidget works.
        // Cell at (wbContentLeft, wbContentTop) = (3, 3) should be inside Window B's red area
        // and NOT covered by Window A (which starts at col 12).
        {
            var sanityCell = snapshot.GetCell(wbContentLeft, wbContentTop);
            Assert.True(sanityCell.Background.HasValue,
                $"Sanity check failed: Window B content cell at ({wbContentLeft},{wbContentTop}) " +
                $"should have a background color but has none.\n" +
                $"Cell: '{sanityCell.Character}'\n" +
                $"Screen:\n{DumpScreen(snapshot, terminalHeight, terminalWidth)}");
            Assert.Equal(WindowBContentBackground.R, sanityCell.Background.Value.R);
            Assert.Equal(WindowBContentBackground.G, sanityCell.Background.Value.G);
            Assert.Equal(WindowBContentBackground.B, sanityCell.Background.Value.B);
        }

        // Now check every border cell of Window A that falls within Window B's content area.
        // Window A absolute screen positions:
        //   Top border:    row=windowAAbsY,   cols=windowAAbsX..windowAAbsX+windowAWidth-1
        //   Bottom border: row=windowAAbsY+windowAHeight-1
        //   Left border:   col=windowAAbsX
        //   Right border:  col=windowAAbsX+windowAWidth-1

        var bleedCells = new List<string>();
        var checkedCellCount = 0;

        // Helper: check if a cell has Window B's red background (indicating bleed-through)
        bool IsRedBackground(TerminalCell cell)
        {
            return cell.Background.HasValue
                && cell.Background.Value.R == WindowBContentBackground.R
                && cell.Background.Value.G == WindowBContentBackground.G
                && cell.Background.Value.B == WindowBContentBackground.B;
        }

        void CheckBorderCell(int x, int y, string borderPart)
        {
            // Only check if this position is within Window B's content area (the red region)
            if (x < wbContentLeft || x > wbContentRight) return;
            if (y < wbContentTop || y > wbContentBottom) return;

            checkedCellCount++;
            var cell = snapshot.GetCell(x, y);
            if (IsRedBackground(cell))
            {
                var bg = cell.Background!.Value;
                bleedCells.Add(
                    $"({x},{y}) '{cell.Character}' [{borderPart}] " +
                    $"bg=rgb({bg.R},{bg.G},{bg.B})");
            }
        }

        // Check Window A's TOP border (row = windowAAbsY)
        for (int x = windowAAbsX; x < windowAAbsX + windowAWidth; x++)
        {
            CheckBorderCell(x, windowAAbsY, "top-border");
        }

        // Check Window A's LEFT border (col = windowAAbsX)
        for (int y = windowAAbsY; y < windowAAbsY + windowAHeight; y++)
        {
            CheckBorderCell(windowAAbsX, y, "left-border");
        }

        // Check Window A's BOTTOM border (row = windowAAbsY + windowAHeight - 1)
        {
            int bottomRow = windowAAbsY + windowAHeight - 1;
            for (int x = windowAAbsX; x < windowAAbsX + windowAWidth; x++)
            {
                CheckBorderCell(x, bottomRow, "bottom-border");
            }
        }

        // Check Window A's RIGHT border (col = windowAAbsX + windowAWidth - 1)
        {
            int rightCol = windowAAbsX + windowAWidth - 1;
            for (int y = windowAAbsY; y < windowAAbsY + windowAHeight; y++)
            {
                CheckBorderCell(rightCol, y, "right-border");
            }
        }

        // Sanity check 2: We must have checked a non-zero number of cells.
        // If this fails, our overlap geometry is wrong and the test is vacuous.
        Assert.True(checkedCellCount > 0,
            $"No overlap cells were checked! Verify test geometry.\n" +
            $"Window A borders: top={windowAAbsY}, bottom={windowAAbsY + windowAHeight - 1}, " +
            $"left={windowAAbsX}, right={windowAAbsX + windowAWidth - 1}\n" +
            $"Window B content: top={wbContentTop}, bottom={wbContentBottom}, " +
            $"left={wbContentLeft}, right={wbContentRight}");

        // Main assertion: no border cells should have Window B's red background.
        if (bleedCells.Count > 0)
        {
            var diagnostics = new System.Text.StringBuilder();
            diagnostics.AppendLine("=== WINDOW BORDER BACKGROUND BLEED-THROUGH DETECTED ===");
            diagnostics.AppendLine();
            diagnostics.AppendLine($"Window B's red background (RGB {WindowBContentBackground.R},{WindowBContentBackground.G},{WindowBContentBackground.B})");
            diagnostics.AppendLine($"was found on {bleedCells.Count} of {checkedCellCount} border cell(s) of Window A:");
            diagnostics.AppendLine();
            foreach (var cell in bleedCells)
            {
                diagnostics.AppendLine($"  • {cell}");
            }
            diagnostics.AppendLine();
            diagnostics.AppendLine("Screen content:");
            diagnostics.AppendLine(DumpScreen(snapshot, terminalHeight, terminalWidth));
            diagnostics.AppendLine();
            diagnostics.AppendLine($"Window A top border row detail (row {windowAAbsY}):");
            diagnostics.AppendLine(DumpRow(snapshot, windowAAbsY, windowAAbsX, windowAAbsX + windowAWidth));
            diagnostics.AppendLine();
            diagnostics.AppendLine($"Window A left border column detail (col {windowAAbsX}):");
            diagnostics.AppendLine(DumpColumn(snapshot, windowAAbsX, windowAAbsY, windowAAbsY + windowAHeight));

            Assert.Fail(diagnostics.ToString());
        }
    }

    /// <summary>
    /// Dumps a row of cells with their character and background color for diagnostics.
    /// </summary>
    private static string DumpRow(Automation.IHex1bTerminalRegion snap, int row, int startCol, int endCol)
    {
        var sb = new System.Text.StringBuilder();
        for (int x = startCol; x < endCol; x++)
        {
            var c = snap.GetCell(x, row);
            var bg = c.Background.HasValue
                ? $"rgb({c.Background.Value.R},{c.Background.Value.G},{c.Background.Value.B})"
                : "none";
            sb.AppendLine($"    [{x},{row}] '{c.Character}' bg={bg}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Dumps a column of cells with their character and background color for diagnostics.
    /// </summary>
    private static string DumpColumn(Automation.IHex1bTerminalRegion snap, int col, int startRow, int endRow)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = startRow; y < endRow; y++)
        {
            var c = snap.GetCell(col, y);
            var bg = c.Background.HasValue
                ? $"rgb({c.Background.Value.R},{c.Background.Value.G},{c.Background.Value.B})"
                : "none";
            sb.AppendLine($"    [{col},{y}] '{c.Character}' bg={bg}");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Dumps the entire screen content for diagnostics.
    /// </summary>
    private static string DumpScreen(Automation.IHex1bTerminalRegion snap, int rows, int cols)
    {
        var sb = new System.Text.StringBuilder();
        for (int y = 0; y < rows; y++)
        {
            sb.Append($"{y,2}|");
            for (int x = 0; x < cols; x++)
            {
                var c = snap.GetCell(x, y);
                sb.Append(string.IsNullOrEmpty(c.Character) ? " " : c.Character);
            }
            sb.AppendLine("|");
        }
        return sb.ToString();
    }
}
