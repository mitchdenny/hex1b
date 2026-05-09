using System.Globalization;
using Spectre.Tui;
using Spectre.Tui.App;
using Color = Spectre.Console.Color;
using Decoration = Spectre.Console.Decoration;
using Style = Spectre.Console.Style;

namespace SpectreTuiDemo;

/// <summary>
/// Base class for an individual demo tab. Implements <see cref="IWidget"/> so
/// the tab can be rendered directly via <c>context.Render(tab, bounds)</c>.
/// </summary>
internal abstract class DemoTab : IWidget
{
    public abstract string TabLabel { get; }
    public abstract string HelpMarkup { get; }

    public virtual void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
    }

    public virtual void Update(FrameInfo frame, IRenderBounds bounds)
    {
    }

    public abstract void Render(RenderContext context);
}

/// <summary>
/// Tab 1 — Welcome. Demonstrates Paragraph + BoxWidget + Layout.
/// </summary>
internal sealed class WelcomeTab : DemoTab
{
    public override string TabLabel => "Welcome";
    public override string HelpMarkup => "[grey]press[/] [aqua]Tab[/] [grey]to advance[/]";

    public override void Render(RenderContext context)
    {
        context.Render(
            new BoxWidget()
                .Border(Border.Rounded)
                .Style(new Style(Color.Green))
                .TitlePadding(1)
                .MarkupTitle("[bold yellow]Hex1b \u2764 Spectre TUI[/]")
                .Inner(
                    new PaddingWidget(
                        new Padding(2, 1),
                        Paragraph.FromMarkup(
                            """
                            [grey]Welcome to the[/] [bold aqua]Spectre.Tui[/] [grey]widget showcase running inside a[/] [bold aqua]Hex1b[/] [grey]workload.[/]

                            Each tab walks through one of Spectre.Tui's primitive widgets:

                              [yellow]List[/]      ListWidget with a vertical scrollbar
                              [yellow]Table[/]     TableWidget with star-sized columns
                              [yellow]Scroll[/]    ScrollViewWidget over long markup text
                              [yellow]Sparkline[/] streaming SparklineWidget + ProgressBar

                            The chrome above shows the [bold]TabsWidget[/], an animated
                            [bold]ProgressBarWidget[/] (with a wave brush), and a [bold]SpinnerWidget[/]
                            in the bottom-right corner.

                            [grey]All of this runs through the Hex1b adapter, so the entire
                            session can be recorded as an asciinema cast (try [/][yellow]--headless[/][grey])
                            or driven by a Hex1bTerminalAutomator (try [/][yellow]--auto[/][grey]).[/]
                            """)
                            .Centered())));
    }
}

/// <summary>
/// Tab 2 — List. Demonstrates ListWidget + ScrollbarWidget + CompositeWidget.
/// </summary>
internal sealed class ListTab : DemoTab
{
    private readonly ListWidget<TodoItem> _list;

    public override string TabLabel => "List";
    public override string HelpMarkup => "[aqua]Up/Down[/]:move  [aqua]Space[/]:toggle";

    public ListTab()
    {
        var items = new List<TodoItem>
        {
            new("Wire up the [bold]Hex1b[/] terminal adapter"),
            new("Implement [italic]Spectre.Console[/] bridge", completed: true),
            new("Implement [italic]Spectre.Tui[/] bridge", completed: true),
            new("Build the [yellow]widget showcase[/]"),
            new("Drive the demo with [yellow]Hex1bTerminalAutomator[/]"),
            new("Record an [aqua]asciinema[/] cast in headless mode"),
            new("Mux the session over [aqua]HMP1[/]"),
            new("Embed Hex1b inside a [italic]Spectre.Tui[/] popup"),
            new("Add a [yellow]Blazor[/] integration"),
            new("Add a [yellow]WinUI3[/] integration"),
            new("Ship the package to NuGet"),
        };

        _list = new ListWidget<TodoItem>(items)
            .HighlightSymbol("> ")
            .HighlightStyle(new Style(Color.Black, Color.Yellow, Decoration.Bold))
            .WrapAround()
            .SelectedIndex(0);
    }

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is not KeyMessage key)
        {
            return;
        }

        switch (key.Info.Key)
        {
            case ConsoleKey.UpArrow: _list.MoveUp(); break;
            case ConsoleKey.DownArrow: _list.MoveDown(); break;
            case ConsoleKey.Spacebar: _list.SelectedItem?.Toggle(); break;
        }
    }

    public override void Render(RenderContext context)
    {
        context.Render(
            new BoxWidget()
                .Border(Border.Rounded)
                .Style(new Style(Color.Green))
                .TitlePadding(1)
                .MarkupTitle("[bold yellow]To-do[/] [grey](ListWidget + ScrollbarWidget)[/]")
                .Inner(new CompositeWidget(
                    new PaddingWidget(new Padding(1, 0, 2, 0), _list),
                    new ScrollbarWidget()
                        .VerticalRight()
                        .Position(_list.SelectedIndex ?? 0)
                        .Length(_list.Items.Count)
                        .ViewportLength(1)
                        .Style(Color.Grey)
                        .ThumbStyle(Color.Aqua))));
    }

    private sealed class TodoItem(string label, bool completed = false) : IListWidgetItem
    {
        private bool _completed = completed;

        public void Toggle() => _completed = !_completed;

        public Text CreateText(bool isSelected)
        {
            var prefix = _completed ? "[green][[x]][/]" : "[grey][[ ]][/]";
            var body = _completed ? $"[strikethrough grey]{label}[/]" : label;
            return Text.FromMarkup($"{prefix} {body}");
        }
    }
}

/// <summary>
/// Tab 3 — Table. Demonstrates TableWidget with custom columns + selection.
/// </summary>
internal sealed class TableTab : DemoTab
{
    private readonly TableWidget<City> _table;

    public override string TabLabel => "Table";
    public override string HelpMarkup => "[aqua]Up/Down[/]:move";

    public TableTab()
    {
        var cities = new[]
        {
            new City(1, "Tokyo", "Japan", 37_400_068),
            new City(2, "Delhi", "India", 32_941_308),
            new City(3, "Shanghai", "China", 28_516_904),
            new City(4, "Dhaka", "Bangladesh", 23_210_000),
            new City(5, "Sao Paulo", "Brazil", 22_806_704),
            new City(6, "Cairo", "Egypt", 22_183_200),
            new City(7, "Mexico City", "Mexico", 22_085_140),
            new City(8, "Beijing", "China", 21_766_214),
            new City(9, "Mumbai", "India", 21_296_517),
            new City(10, "Osaka", "Japan", 19_222_665),
            new City(11, "Karachi", "Pakistan", 17_236_000),
            new City(12, "Istanbul", "Turkey", 15_848_000),
        };

        _table = new TableWidget<City>([.. cities])
            .AutoAddColumns()
            .HeaderStyle(new Style(Color.Yellow, decoration: Decoration.Bold))
            .HighlightStyle(new Style(Color.Black, Color.Aqua, Decoration.Bold))
            .WrapAround()
            .SelectedIndex(0);
    }

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is not KeyMessage key)
        {
            return;
        }

        switch (key.Info.Key)
        {
            case ConsoleKey.UpArrow: _table.MoveUp(); break;
            case ConsoleKey.DownArrow: _table.MoveDown(); break;
        }
    }

    public override void Render(RenderContext context)
    {
        context.Render(
            new BoxWidget()
                .Border(Border.Rounded)
                .Style(new Style(Color.Green))
                .TitlePadding(1)
                .MarkupTitle("[bold yellow]Largest cities[/] [grey](TableWidget)[/]")
                .Inner(new PaddingWidget(new Padding(1, 0), _table)));
    }

    private sealed class City(int rank, string name, string country, long population)
        : TableRow, ITableColumnDefinition
    {
        public int Rank { get; } = rank;
        public string Name { get; } = name;
        public string Country { get; } = country;
        public long Population { get; } = population;

        public static IEnumerable<TableColumn> GetColumns() =>
        [
            new TableColumn("Rank"),
            new TableColumn("City").StarWidth(2),
            new TableColumn("Country").StarWidth(1),
            new TableColumn("Population").RightAligned(),
        ];

        protected override Text[] CreateCells(bool isSelected) =>
        [
            Text.FromMarkup($"[italic]{Rank.ToString(CultureInfo.InvariantCulture)}[/]"),
            Text.FromString(Name),
            Text.FromString(Country),
            Text.FromString(Population.ToString("N0", CultureInfo.InvariantCulture)),
        ];
    }
}

/// <summary>
/// Tab 4 — Scroll. Demonstrates ScrollViewWidget over a long markup paragraph.
/// </summary>
internal sealed class ScrollTab : DemoTab
{
    private readonly ScrollViewWidget _scroll;

    public override string TabLabel => "Scroll";
    public override string HelpMarkup => "[aqua]Up/Down[/]:line  [aqua]PgUp/PgDn[/]:page  [aqua]Home/End[/]:edge";

    public ScrollTab()
    {
        _scroll = new ScrollViewWidget()
            .Inner(
                Paragraph.FromMarkup(
                    """
                    [bold yellow]Why a Spectre.Tui bridge?[/]

                    Spectre.Tui is a layered library: the core ships render primitives
                    ([italic]BoxWidget[/], [italic]ListWidget[/], [italic]TableWidget[/], [italic]ScrollViewWidget[/], [italic]SparklineWidget[/], etc.)
                    while [bold]Spectre.Tui.App[/] adds a screen stack, an input pump, and a
                    target-fps render loop on top.

                    The Hex1b bridge plugs into both layers. [bold]Hex1bSpectreTuiTerminal[/]
                    implements [italic]Spectre.Tui.ITerminal[/] by forwarding every flushed ANSI
                    chunk into a [bold]IHex1bAppTerminalWorkloadAdapter[/], so anything Spectre.Tui
                    renders becomes a recordable, muxable, embeddable Hex1b stream.

                    On the input side, [bold]Hex1bSpectreTuiInputReader[/] implements
                    [italic]IInputReader[/] over the workload adapter's input channel. Because
                    [italic]Hex1bTerminalAutomator[/] writes into the same channel, an automator
                    script can drive a Spectre.Tui app exactly like it drives any other
                    Hex1b workload — no extra plumbing.

                    [bold yellow]Layout primitives[/]

                    Spectre.Tui's [italic]Layout[/] type splits a region into named child slots,
                    either by row or by column, with optional fixed sizes. The chrome
                    above this tab uses a five-row layout (title, tabs, progress, body,
                    status) where the inner three rows are 1 cell tall and the body
                    expands to fill the rest.

                    [bold yellow]Composite widgets[/]

                    The [italic]CompositeWidget[/] stacks multiple widgets in the same region —
                    you'll see it on the List tab, where a [italic]ListWidget[/] and a
                    [italic]ScrollbarWidget[/] occupy the same box.

                    [bold yellow]Animation[/]

                    Spectre.Tui calls [italic]Update(frame, bounds)[/] once per render frame with
                    the elapsed wall-clock time. The [italic]SparklineWidget[/] tab uses this hook
                    to push a new random sample into a fixed-size ring buffer every 50 ms,
                    while the [italic]ProgressBarWidget[/] above oscillates between 0 and 100 with
                    a wave brush that blends two RGB colours along its length.

                    [bold yellow]End of tour[/]

                    Press [aqua]Tab[/] to keep advancing through the demo, or [aqua]Q[/] / [aqua]Esc[/] to quit.
                    """)
                    .Folded())
            .HorizontalScroll(ScrollMode.Disabled)
            .ScrollbarStyle(Color.Grey)
            .ScrollbarThumbStyle(Color.Aqua);
    }

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is not KeyMessage key)
        {
            return;
        }

        switch (key.Info.Key)
        {
            case ConsoleKey.UpArrow: _scroll.ScrollUp(); break;
            case ConsoleKey.DownArrow: _scroll.ScrollDown(); break;
            case ConsoleKey.PageUp: _scroll.PageUp(); break;
            case ConsoleKey.PageDown: _scroll.PageDown(); break;
            case ConsoleKey.Home: _scroll.ScrollToTop(); break;
            case ConsoleKey.End: _scroll.ScrollToBottom(); break;
        }
    }

    public override void Render(RenderContext context)
    {
        context.Render(
            new BoxWidget()
                .Border(Border.Rounded)
                .Style(new Style(Color.Green))
                .TitlePadding(1)
                .MarkupTitle("[bold yellow]Scrollable text[/] [grey](ScrollViewWidget)[/]")
                .Inner(new PaddingWidget(new Padding(1, 0), _scroll)));
    }
}

/// <summary>
/// Tab 5 — Sparkline. Demonstrates an animated SparklineWidget that pushes a
/// fresh random sample into its ring buffer every render tick.
/// </summary>
internal sealed class SparklineTab : DemoTab
{
    private const int Capacity = 256;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(50);

    private readonly SparklineWidget _sparkline;
    private TimeSpan _accumulated;

    public override string TabLabel => "Sparkline";
    public override string HelpMarkup => "[grey]auto-streams; press[/] [aqua]Tab[/] [grey]to leave[/]";

    public SparklineTab()
    {
        _sparkline = new SparklineWidget()
            .Direction(SparklineDirection.RightToLeft)
            .Style(new Style(Color.Aqua));
    }

    public override void Update(FrameInfo frame, IRenderBounds bounds)
    {
        _accumulated += frame.FrameTime;
        while (_accumulated >= SampleInterval)
        {
            _accumulated -= SampleInterval;
            _sparkline.Data.Add((ulong)Random.Shared.Next(0, 100));
            if (_sparkline.Data.Count > Capacity)
            {
                _sparkline.Data.RemoveAt(0);
            }
        }
    }

    public override void Render(RenderContext context)
    {
        context.Render(
            new BoxWidget()
                .Border(Border.Rounded)
                .Style(new Style(Color.Green))
                .TitlePadding(1)
                .MarkupTitle("[bold yellow]Streaming samples[/] [grey](SparklineWidget)[/]")
                .Inner(new PaddingWidget(new Padding(1, 0), _sparkline)));
    }
}
