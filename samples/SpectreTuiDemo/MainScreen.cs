using Spectre.Tui;
using Spectre.Tui.App;
using Color = Spectre.Console.Color;
using Decoration = Spectre.Console.Decoration;
using Style = Spectre.Console.Style;

namespace SpectreTuiDemo;

/// <summary>
/// The top-level Spectre.Tui screen for the demo. Renders a tab strip at the
/// top, one of the five <see cref="DemoTab"/> implementations in the middle,
/// and a help/spinner status bar at the bottom. A help popup is pushed onto
/// the application stack when the user presses '?'.
/// </summary>
internal sealed class MainScreen : Screen
{
    /// <summary>
    /// The canonical tab order. Exposed so <see cref="DemoAutomator"/> can
    /// drive the same set in the same order without duplicating the list.
    /// </summary>
    public static IReadOnlyList<string> TabLabels { get; } =
    [
        "Welcome",
        "List",
        "Table",
        "Scroll",
        "Sparkline",
    ];

    private readonly DemoTab[] _tabs;
    private readonly TabsWidget<TabHeader> _tabBar;
    private readonly SpinnerWidget _spinner;
    private readonly ProgressBarWidget _progress;
    private readonly Layout _layout;

    private double _progressDirection = 1.0;

    public MainScreen()
    {
        _tabs =
        [
            new WelcomeTab(),
            new ListTab(),
            new TableTab(),
            new ScrollTab(),
            new SparklineTab(),
        ];

        _tabBar = new TabsWidget<TabHeader>()
            .Items(TabLabels.Select(label => new TabHeader(label)))
            .HighlightStyle(new Style(Color.Black, Color.Aqua, Decoration.Bold))
            .WrapAround()
            .SelectedIndex(0);

        _spinner = new SpinnerWidget().Kind(SpinnerKind.Default);

        _progress = new ProgressBarWidget()
            .Value(0)
            .Max(100)
            .Foreground(ProgressBarBrush.Wave(new Color(177, 79, 255), new Color(0, 255, 163)))
            .HideLabel()
            .Smooth();

        // Top: title (1 row) + tabs (1 row) + progress (1 row)
        // Middle: active tab content
        // Bottom: status bar (1 row)
        _layout = new Layout("Root")
            .SplitRows(
                new Layout("Title").Size(1),
                new Layout("Tabs").Size(1),
                new Layout("Progress").Size(1),
                new Layout("Body"),
                new Layout("Status").Size(1));
    }

    private DemoTab ActiveTab => _tabs[_tabBar.SelectedIndex];

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is not KeyMessage key)
        {
            return;
        }

        switch (key.Info.Key)
        {
            case ConsoleKey.Q:
            case ConsoleKey.Escape:
                context.Quit();
                return;

            case ConsoleKey.Tab:
                if ((key.Info.Modifiers & ConsoleModifiers.Shift) != 0)
                {
                    _tabBar.MoveLeft();
                }
                else
                {
                    _tabBar.MoveRight();
                }
                return;

            case ConsoleKey.Oem2: // '?' on US keyboards (also '/' without shift)
                if (key.Info.KeyChar == '?')
                {
                    context.Push(new HelpPopupScreen());
                    return;
                }
                break;
        }

        // '?' may also arrive as a plain character event without a recognised
        // key code (mapper round-trips through ConsoleKey.None for symbols).
        if (key.Info.KeyChar == '?')
        {
            context.Push(new HelpPopupScreen());
            return;
        }

        ActiveTab.OnMessage(context, message);
    }

    public override void Update(FrameInfo frame, IRenderBounds bounds)
    {
        ActiveTab.Update(frame, bounds);

        _spinner.Update(frame);
        _progress.Update(frame);

        _progress.Value += _progressDirection * 35.0 * frame.FrameTime.TotalSeconds;
        if (_progress.Value >= _progress.Max)
        {
            _progress.Value = _progress.Max;
            _progressDirection = -1.0;
        }
        else if (_progress.Value <= 0.0)
        {
            _progress.Value = 0.0;
            _progressDirection = 1.0;
        }
    }

    public override void Render(RenderContext context)
    {
        context.Render(new ClearWidget());

        // Title
        context.Render(
            Paragraph.FromMarkup("[bold aqua]Hex1b[/] [grey]x[/] [bold yellow]Spectre.Tui[/] [grey]widget showcase[/]")
                .Centered(),
            _layout.GetArea(context, "Title"));

        // Tabs
        context.Render(_tabBar, _layout.GetArea(context, "Tabs"));

        // Animated progress strip (chrome, not the tab content)
        context.Render(_progress, _layout.GetArea(context, "Progress"));

        // Active tab body
        context.Render(ActiveTab, _layout.GetArea(context, "Body"));

        // Status bar at the bottom: help text on the left, spinner on the right
        var statusArea = _layout.GetArea(context, "Status");
        var spinnerWidth = 2;
        var helpArea = new Rectangle(statusArea.X, statusArea.Y, statusArea.Width - spinnerWidth - 1, statusArea.Height);
        var spinnerArea = new Rectangle(statusArea.X + statusArea.Width - spinnerWidth, statusArea.Y, spinnerWidth, statusArea.Height);

        var help = $"[grey]Tab:next  Shift+Tab:prev  ?:help  Q/Esc:quit[/]  [grey]|[/]  {ActiveTab.HelpMarkup}";
        context.Render(
            Paragraph.FromMarkup(help)
                .Style(new Style(Color.Grey)),
            helpArea);

        context.Render(_spinner, spinnerArea);
    }

    /// <summary>
    /// Minimal <see cref="ITabWidgetItem"/> implementation for tab labels.
    /// </summary>
    private sealed class TabHeader(string label) : ITabWidgetItem
    {
        public TextLine CreateTextLine(bool isSelected)
            => TextLine.FromMarkup(isSelected ? $"[bold]{label}[/]" : label);
    }
}

/// <summary>
/// A simple help popup. Pushed onto the application stack when the user
/// presses '?', popped on Escape.
/// </summary>
internal sealed class HelpPopupScreen : Screen
{
    public override bool IsTransparent => true;

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is KeyMessage { Info.Key: ConsoleKey.Escape or ConsoleKey.Q })
        {
            context.Pop();
        }
    }

    public override void Render(RenderContext context)
    {
        context.Render(
            new PopupWidget(new Size(48, 11))
                .Backdrop(new BackdropWidget())
                .Content(
                    new BoxWidget()
                        .Border(Border.Rounded)
                        .Style(new Style(Color.Aqua))
                        .TitlePadding(1)
                        .MarkupTitle("[bold yellow]Help[/]")
                        .Inner(
                            new PaddingWidget(
                                new Padding(2, 1),
                                Paragraph.FromMarkup(
                                    """
                                    [aqua]Tab[/] / [aqua]Shift+Tab[/]  switch tabs
                                    [aqua]?[/]                  this popup
                                    [aqua]Q[/] or [aqua]Esc[/]         quit

                                    Each tab demonstrates a different
                                    Spectre.Tui widget — table, list,
                                    scrollable text, sparkline, and a
                                    paragraph in a box.

                                    [grey]Press [/][yellow]Esc[/][grey] to close.[/]
                                    """)
                                    .Centered()))));
    }
}
