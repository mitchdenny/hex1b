using Hex1b;
using Hex1b.Integrations.Spectre.SpectreTui;
using Hex1b.Widgets;
using Spectre.Tui;
using Spectre.Tui.App;
using Color = Spectre.Console.Color;
using HexBorderWidget = Hex1b.Widgets.BorderWidget;
using HexSurfaceWidget = Hex1b.Widgets.SurfaceWidget;
using HexWidget = Hex1b.Widgets.Hex1bWidget;
using SpinnerWidget = Spectre.Tui.SpinnerWidget;
using Style = Spectre.Console.Style;

namespace Hex1bInsideSpectreTui;

/// <summary>
/// The Spectre.Tui screen that wraps the embedded Hex1b widget. The Spectre
/// half owns the title bar, a left-side info column with a SparklineWidget,
/// and the bottom status bar. The centre body region is rendered by a
/// <see cref="Hex1bSpectreTuiWidget"/> hosting a real Hex1b
/// <see cref="HexSurfaceWidget"/> whose layer callback draws the GlobeDemo
/// globe (auto-rotating, with cloud drift, contour terrain, and POI labels).
/// Arrow keys / +/- forwarded from Spectre.Tui's input loop adjust the
/// rotation and zoom directly on the persisted <see cref="Globe"/> state.
/// </summary>
internal sealed class EmbedScreen : Screen
{
    private const float ArrowYawRadians = 0.18f;
    private const float ArrowPitchRadians = 0.14f;

    private readonly Globe _globe = new();
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

        _layout = new Layout("Root")
            .SplitRows(
                new Layout("Title").Size(1),
                new Layout("Body")
                    .SplitColumns(
                        new Layout("Info").Size(36),
                        new Layout("Hex1b")),
                new Layout("Status").Size(1));
    }

    public override void OnMessage(ApplicationContext context, ApplicationMessage message)
    {
        if (message is not KeyMessage key)
        {
            return;
        }

        if (key.Info.Key is ConsoleKey.Q or ConsoleKey.Escape)
        {
            context.Quit();
            return;
        }

        // Arrow keys spin the globe; +/- zoom in/out. These mutate state owned
        // by the Globe instance, which the SurfaceWidget layer reads on every
        // Spectre.Tui frame.
        switch (key.Info.Key)
        {
            case ConsoleKey.LeftArrow:
                _globe.Yaw(-ArrowYawRadians);
                return;
            case ConsoleKey.RightArrow:
                _globe.Yaw(ArrowYawRadians);
                return;
            case ConsoleKey.UpArrow:
                _globe.Pitch(-ArrowPitchRadians);
                return;
            case ConsoleKey.DownArrow:
                _globe.Pitch(ArrowPitchRadians);
                return;
            case ConsoleKey.OemPlus or ConsoleKey.Add:
                _globe.ScaleZoom(1.15);
                return;
            case ConsoleKey.OemMinus or ConsoleKey.Subtract:
                _globe.ScaleZoom(0.87);
                return;
        }

        // Otherwise forward unhandled keys to the embedded Hex1b widget.
        _embeddedHex1b.HandleKey(key.Info);
    }

    public override void Update(FrameInfo frame, IRenderBounds bounds)
    {
        // Tick the globe so the auto-rotate spin and cloud drift advance even
        // when no key has been pressed.
        _globe.Tick(frame.FrameTime.TotalSeconds);

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
            Paragraph.FromMarkup("[bold aqua]Hex1b[/] [red]\u2764[/] [bold yellow]Spectre TUI[/] [grey]embedded GlobeDemo[/]")
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
                                Paragraph + SparklineWidget.

                                The body to the right is rendered by a
                                [yellow]Hex1bSpectreTuiWidget[/] hosting a Hex1b
                                [yellow]SurfaceWidget[/] that draws the
                                [aqua]GlobeDemo[/] globe — braille terrain
                                contours, drifting clouds with shadow
                                projection, and POI labels. Auto-rotates
                                between frames.

                                Use [yellow]arrows[/] to spin and
                                [yellow]+/-[/] to zoom.
                                """)
                                .Style(new Style(Color.Grey)),
                            _sparkline,
                        ]))),
            infoArea);

        context.Render(_embeddedHex1b, _layout.GetArea(context, "Hex1b"));

        var statusArea = _layout.GetArea(context, "Status");
        var spinnerWidth = 2;
        var helpArea = new Rectangle(statusArea.X, statusArea.Y, statusArea.Width - spinnerWidth - 1, statusArea.Height);
        var spinnerArea = new Rectangle(statusArea.X + statusArea.Width - spinnerWidth, statusArea.Y, spinnerWidth, statusArea.Height);

        context.Render(
            Paragraph.FromMarkup("[grey]Arrows spin globe   +/- zoom   Q/Esc quit[/]")
                .Style(new Style(Color.Grey)),
            helpArea);

        context.Render(_spinner, spinnerArea);
    }

    private HexWidget BuildEmbeddedWidget()
    {
        return new HexBorderWidget(
                new HexSurfaceWidget(s => [s.Layer(_globe.Draw)]))
            .Title("Hex1b SurfaceWidget — GlobeDemo embed");
    }
}
