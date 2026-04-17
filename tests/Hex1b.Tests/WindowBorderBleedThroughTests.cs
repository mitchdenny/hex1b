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
    /// Verifies that window border background is uniform around all edges, including
    /// the title bar row. The left and right border cells adjacent to the title bar
    /// must have the same background as all other border cells (the content background),
    /// NOT the title bar background color.
    /// </summary>
    /// <remarks>
    /// <para><strong>Why this matters:</strong></para>
    /// <para>
    /// The title bar row has a distinct background color (e.g., dark gray) for the inner
    /// content area (window title, close button). But the border characters (│) on the
    /// left and right edges of that row are part of the window's border frame, not the
    /// title bar content. They should match the rest of the border's background color
    /// for a consistent, uniform border appearance.
    /// </para>
    ///
    /// <para><strong>Test layout:</strong></para>
    /// <code>
    ///   ┌────────────────────┐  ← top border:     bg = content bg (uniform)
    ///   │ Window Title     × │  ← title bar row:  border │ = content bg, inner = title bg
    ///   │ Content here       │  ← content row:    border │ = content bg, inner = content bg
    ///   │                    │
    ///   └────────────────────┘  ← bottom border:  bg = content bg (uniform)
    ///
    ///   All border characters should have the SAME background color (content bg).
    ///   The title bar's distinct background should only apply to the INNER area.
    /// </code>
    /// </remarks>
    [Fact]
    public async Task WindowBorder_TitleBarRow_HasUniformBorderBackground()
    {
        const int terminalWidth = 60;
        const int terminalHeight = 20;

        // Window position and size
        const int windowOffsetX = 5;
        const int windowOffsetY = 2;
        const int windowWidth = 40;
        const int windowHeight = 10;

        // Panel starts at row 1 (MenuBar at row 0)
        const int panelStartRow = 1;
        const int windowAbsX = windowOffsetX;
        const int windowAbsY = panelStartRow + windowOffsetY;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(terminalWidth, terminalHeight)
            .Build();

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                ctx.VStack(outer => [
                    outer.MenuBar(m => [
                        m.Menu("Setup", menu => [
                            menu.MenuItem("Open").OnActivated(e =>
                            {
                                var window = e.Windows.Window(w =>
                                    new VStackWidget([
                                        new TextBlockWidget("Test content"),
                                    ]))
                                    .Title("Test Window")
                                    .Size(windowWidth, windowHeight)
                                    .Position(new WindowPositionSpec(WindowPosition.TopLeft, windowOffsetX, windowOffsetY));
                                e.Windows.Open(window);
                            })
                        ])
                    ]),
                    outer.WindowPanel()
                        .Height(SizeHint.Fill)
                ])
            ),
            new Hex1bAppOptions { WorkloadAdapter = workload }
        );

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Open the window via menu
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Setup"), TimeSpan.FromSeconds(5), "menu rendered")
            .Alt().Key(Hex1bKey.S)
            .WaitUntil(s => s.ContainsText("Open"), TimeSpan.FromSeconds(5), "menu opened")
            .Key(Hex1bKey.Enter)
            .Build()
            .ApplyAsync(terminal, TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Test content"), TimeSpan.FromSeconds(5), "window rendered")
            .Wait(TimeSpan.FromMilliseconds(200))
            .Capture("border-uniform-bg")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // Collect the background color of every border cell around the window.
        // All border cells should have the same background color (the content bg).
        //
        // Window layout (with title bar):
        //   Row 0 (abs windowAbsY):   top border     ┌─────────┐
        //   Row 1 (abs windowAbsY+1): title bar row  │ Title × │
        //   Row 2+ (abs windowAbsY+2..): content     │ ...     │
        //   Last row:                  bottom border  └─────────┘

        var borderCellBgs = new List<(int x, int y, string ch, Hex1bColor? bg)>();

        // Top border (row = windowAbsY)
        for (int col = windowAbsX; col < windowAbsX + windowWidth; col++)
        {
            var c = snapshot.GetCell(col, windowAbsY);
            borderCellBgs.Add((col, windowAbsY, c.Character, c.Background));
        }

        // Bottom border (row = windowAbsY + windowHeight - 1)
        int bottomRow = windowAbsY + windowHeight - 1;
        for (int col = windowAbsX; col < windowAbsX + windowWidth; col++)
        {
            var c = snapshot.GetCell(col, bottomRow);
            borderCellBgs.Add((col, bottomRow, c.Character, c.Background));
        }

        // Left border (col = windowAbsX, all rows including title bar)
        for (int row = windowAbsY; row < windowAbsY + windowHeight; row++)
        {
            var c = snapshot.GetCell(windowAbsX, row);
            borderCellBgs.Add((windowAbsX, row, c.Character, c.Background));
        }

        // Right border (col = windowAbsX + windowWidth - 1, all rows including title bar)
        int rightCol = windowAbsX + windowWidth - 1;
        for (int row = windowAbsY; row < windowAbsY + windowHeight; row++)
        {
            var c = snapshot.GetCell(rightCol, row);
            borderCellBgs.Add((rightCol, row, c.Character, c.Background));
        }

        // Find the most common background color among border cells (should be the content bg)
        var bgGroups = borderCellBgs
            .Where(c => c.bg.HasValue)
            .GroupBy(c => (c.bg!.Value.R, c.bg.Value.G, c.bg.Value.B))
            .OrderByDescending(g => g.Count())
            .ToList();

        Assert.True(bgGroups.Count > 0, "No border cells with background color found");

        var expectedBg = bgGroups[0].Key;

        // Assert: every border cell should have the same background
        var mismatchCells = borderCellBgs
            .Where(c => c.bg.HasValue &&
                (c.bg.Value.R != expectedBg.R || c.bg.Value.G != expectedBg.G || c.bg.Value.B != expectedBg.B))
            .Distinct()
            .ToList();

        if (mismatchCells.Count > 0)
        {
            var diagnostics = new System.Text.StringBuilder();
            diagnostics.AppendLine("=== NON-UNIFORM BORDER BACKGROUND DETECTED ===");
            diagnostics.AppendLine();
            diagnostics.AppendLine($"Expected uniform border background: rgb({expectedBg.R},{expectedBg.G},{expectedBg.B})");
            diagnostics.AppendLine($"Found {mismatchCells.Count} border cell(s) with different background:");
            diagnostics.AppendLine();
            foreach (var (cx, cy, ch, bg) in mismatchCells)
            {
                var bgStr = bg.HasValue ? $"rgb({bg.Value.R},{bg.Value.G},{bg.Value.B})" : "none";
                diagnostics.AppendLine($"  • ({cx},{cy}) '{ch}' bg={bgStr}");
            }
            diagnostics.AppendLine();
            diagnostics.AppendLine("All border cells:");
            foreach (var (cx, cy, ch, bg) in borderCellBgs.Distinct())
            {
                var bgStr = bg.HasValue ? $"rgb({bg.Value.R},{bg.Value.G},{bg.Value.B})" : "none";
                diagnostics.AppendLine($"  [{cx},{cy}] '{ch}' bg={bgStr}");
            }
            diagnostics.AppendLine();
            diagnostics.AppendLine("Screen:");
            diagnostics.AppendLine(DumpScreen(snapshot, terminalHeight, terminalWidth));

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
