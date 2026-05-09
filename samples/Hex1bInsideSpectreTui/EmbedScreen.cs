using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using Hex1b.Widgets;
using Spectre.Tui;
using Spectre.Tui.App;
using Color = Spectre.Console.Color;
using HexBorderWidget = Hex1b.Widgets.BorderWidget;
using HexListWidget = Hex1b.Widgets.ListWidget;
using HexPaddingWidget = Hex1b.Widgets.PaddingWidget;
using HexTextBlockWidget = Hex1b.Widgets.TextBlockWidget;
using HexVStackWidget = Hex1b.Widgets.VStackWidget;
using HexWidget = Hex1b.Widgets.Hex1bWidget;
using SpinnerWidget = Spectre.Tui.SpinnerWidget;
using Style = Spectre.Console.Style;

namespace Hex1bInsideSpectreTui;

/// <summary>
/// The Spectre.Tui screen that wraps the embedded Hex1b widget. The Spectre
/// half owns the title bar, a left-side info column with a SparklineWidget,
/// and the bottom status bar. The centre body region is rendered by a
/// <see cref="Hex1bSpectreTuiWidget"/> hosting a Hex1b widget tree (a
/// labelled <see cref="ListWidget"/>) whose selection state survives every
/// Spectre.Tui frame thanks to the embedded host's persisted node tree.
/// </summary>
internal sealed class EmbedScreen : Screen
{
    /// <summary>
    /// Items rendered in the embedded Hex1b list. Exposed so
    /// <see cref="EmbedAutomator"/> can drive arrow keys without duplicating
    /// the list.
    /// </summary>
    public static IReadOnlyList<string> ListItems { get; } =
    [
        "TextBlockWidget",
        "BorderWidget",
        "ListWidget",
        "TextBoxWidget",
        "VStackWidget",
        "HStackWidget",
        "TabPanelWidget",
        "TerminalWidget",
        "TableWidget",
        "PickerWidget",
    ];

    private readonly SparklineWidget _sparkline;
    private readonly SpinnerWidget _spinner;
    private readonly Hex1bSpectreTuiWidget _embeddedHex1b;
    private readonly Layout _layout;

    private TimeSpan _accumulated;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(80);
    private const int SparkCapacity = 64;

    public EmbedScreen()
    {
        _spinner = new SpinnerWidget().Kind(SpinnerKind.Default);

        _sparkline = new SparklineWidget()
            .Direction(SparklineDirection.RightToLeft)
            .Style(new Style(Color.Aqua));

        _embeddedHex1b = new Hex1bSpectreTuiWidget(BuildEmbeddedWidget);

        // Three rows: 1 row title, body, 1 row status bar.
        // Body splits into 32-column left info panel + the embedded Hex1b body.
        _layout = new Layout("Root")
            .SplitRows(
                new Layout("Title").Size(1),
                new Layout("Body")
                    .SplitColumns(
                        new Layout("Info").Size(32),
                        new Layout("Hex1b")),
                new Layout("Status").Size(1));
    }

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is not KeyMessage key)
        {
            return;
        }

        // Q / Escape always quit, regardless of what the embedded tree wants.
        if (key.Info.Key is ConsoleKey.Q or ConsoleKey.Escape)
        {
            context.Quit();
            return;
        }

        // Forward all other keys to the embedded Hex1b widget. If it consumes
        // the event we're done; otherwise we'd handle it at the screen level.
        _embeddedHex1b.HandleKey(key.Info);
    }

    public override void Update(FrameInfo frame, IRenderBounds bounds)
    {
        // Stream a fresh sample into the sparkline so the chrome animates and
        // proves we're still inside Spectre.Tui's render loop while the
        // embedded Hex1b panel handles its own ticks.
        _accumulated += frame.FrameTime;
        while (_accumulated >= SampleInterval)
        {
            _accumulated -= SampleInterval;
            _sparkline.Data.Add((ulong)Random.Shared.Next(0, 100));
            if (_sparkline.Data.Count > SparkCapacity)
            {
                _sparkline.Data.RemoveAt(0);
            }
        }
    }

    public override void Render(RenderContext context)
    {
        context.Render(new ClearWidget());

        context.Render(
            Paragraph.FromMarkup("[bold aqua]Hex1b[/] [red]\u2764[/] [bold yellow]Spectre TUI[/] [grey]embed showcase[/]")
                .Centered(),
            _layout.GetArea(context, "Title"));

        var infoArea = _layout.GetArea(context, "Info");
        context.Render(
            new BoxWidget()
                .Border(Border.Rounded)
                .Style(new Style(Color.Grey))
                .TitlePadding(1)
                .MarkupTitle("[bold yellow]Spectre.Tui chrome[/]")
                .Inner(
                    new global::Spectre.Tui.PaddingWidget(
                        new Padding(1, 1),
                        new CompositeWidget([
                            Paragraph.FromMarkup(
                                """
                                This box is a [aqua]Spectre.Tui[/] BoxWidget +
                                Paragraph + SparklineWidget. The body to the
                                right is rendered by a [yellow]Hex1bSpectreTuiWidget[/]
                                hosting a real Hex1b ListWidget — its
                                selection survives every Spectre.Tui frame.
                                """)
                                .Style(new Style(Color.Grey)),
                            _sparkline,
                        ]))),
            infoArea);

        // The embedded Hex1b widget tree.
        context.Render(_embeddedHex1b, _layout.GetArea(context, "Hex1b"));

        var statusArea = _layout.GetArea(context, "Status");
        var spinnerWidth = 2;
        var helpArea = new Rectangle(statusArea.X, statusArea.Y, statusArea.Width - spinnerWidth - 1, statusArea.Height);
        var spinnerArea = new Rectangle(statusArea.X + statusArea.Width - spinnerWidth, statusArea.Y, spinnerWidth, statusArea.Height);

        context.Render(
            Paragraph.FromMarkup("[grey]Up/Down/PageUp/PageDown navigate Hex1b list  Q/Esc quit[/]")
                .Style(new Style(Color.Grey)),
            helpArea);

        context.Render(_spinner, spinnerArea);
    }

    /// <summary>
    /// The Hex1b widget tree rendered into the embedded panel. Called on
    /// every Spectre.Tui frame; reconciliation against the persisted node
    /// keeps the list's current selection / scroll offset stable.
    /// </summary>
    private static HexWidget BuildEmbeddedWidget()
    {
        return new HexBorderWidget(
            new HexVStackWidget([
                new HexTextBlockWidget("This widget tree is Hex1b — reconciled every frame."),
                new HexTextBlockWidget("Selection / focus state survives every reconcile."),
                new HexTextBlockWidget(""),
                new HexPaddingWidget(1, 1, 0, 0, new HexListWidget(EmbedScreen.ListItems)),
            ]))
            .Title("Hex1b inside Spectre.Tui");
    }
}
