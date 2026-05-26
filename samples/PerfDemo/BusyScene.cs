using Hex1b;
using Hex1b.Layout;
using Hex1b.Widgets;

namespace PerfDemo;

/// <summary>
/// Builds a representative "busy" widget tree for performance profiling.
///
/// Layout:
///   ┌── HSplitter ───────────────────────────────────────────────┐
///   │  Left column (sidebar)         │  Main pane                │
///   │  Border + VStack of N entries  │  VSplitter:               │
///   │  - one row mutates per frame   │   Top: dashboard grid     │
///   │                                 │   Bot: scrolling log     │
///   └─────────────────────────────────────────────────────────────┘
///
/// Cell-level changes per frame are bounded but spread across multiple
/// non-cached subtrees so the surface diff, token emission, and ANSI
/// serialize phases all do real work.
/// </summary>
internal static class BusyScene
{
    public static Hex1bWidget Build(int frame, int sidebarItems, int logLines, int gridRows, int gridCols)
    {
        var sidebar = BuildSidebar(frame, sidebarItems);
        var dashboard = BuildDashboard(frame, gridRows, gridCols);
        var log = BuildLog(frame, logLines);

        var mainPane = new BorderWidget(
            new SplitterWidget(
                first: new BorderWidget(dashboard).Title("Dashboard"),
                second: new BorderWidget(log).Title("Log"),
                firstSize: Math.Max(6, gridRows + 2),
                orientation: SplitterOrientation.Vertical));

        var root = new SplitterWidget(
            first: new BorderWidget(sidebar).Title("Navigation"),
            second: mainPane,
            firstSize: 28,
            orientation: SplitterOrientation.Horizontal);

        var header = new TextBlockWidget($"Hex1b PerfDemo — busy frame {frame:N0}");

        return new VStackWidget(new Hex1bWidget[]
        {
            header,
            root,
        });
    }

    private static Hex1bWidget BuildSidebar(int frame, int items)
    {
        var children = new Hex1bWidget[items];
        var selected = frame % items;
        for (var i = 0; i < items; i++)
        {
            // Only the "selected" row visibly changes per frame; the rest are static.
            var marker = i == selected ? "▶" : " ";
            children[i] = new TextBlockWidget($"{marker} item-{i:000}  ");
        }
        return new VStackWidget(children);
    }

    private static Hex1bWidget BuildDashboard(int frame, int rows, int cols)
    {
        var rowWidgets = new Hex1bWidget[rows];
        for (var r = 0; r < rows; r++)
        {
            var cells = new Hex1bWidget[cols];
            for (var c = 0; c < cols; c++)
            {
                // Phase shift per cell so we exercise scattered diffs, not a single
                // contiguous run.
                var v = (frame + (r * 17) + (c * 29)) % 100;
                cells[c] = new TextBlockWidget($" {v,3} ");
            }
            rowWidgets[r] = new HStackWidget(cells);
        }

        // A progress bar that advances every frame to add another small diff target.
        var progress = new ProgressWidget
        {
            Value = (frame % 100),
            Minimum = 0,
            Maximum = 100
        };

        return new VStackWidget(new Hex1bWidget[]
        {
            progress,
            new VStackWidget(rowWidgets)
        });
    }

    private static Hex1bWidget BuildLog(int frame, int lines)
    {
        // Scrolling log: every line moves up by one each frame, so every visible
        // row changes. This is the most expensive subtree.
        var children = new Hex1bWidget[lines];
        for (var i = 0; i < lines; i++)
        {
            var n = frame - lines + i + 1;
            children[i] = new TextBlockWidget($"{n:0000000}  evt={(n * 1103515245 + 12345) & 0xFFFF:X4}  msg=line content {i}");
        }
        return new VStackWidget(children);
    }
}
