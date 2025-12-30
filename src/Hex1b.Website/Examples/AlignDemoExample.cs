using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Align Widget Documentation: Interactive Demo
/// Demonstrates selecting different alignments and seeing them applied.
/// </summary>
public class AlignDemoExample(ILogger<AlignDemoExample> logger) : Hex1bExample
{
    private readonly ILogger<AlignDemoExample> _logger = logger;

    public override string Id => "align-demo";
    public override string Title => "Align Widget - Interactive Demo";
    public override string Description => "Select different alignments to see them applied to content in real-time.";

    private class AlignDemoState
    {
        public Alignment CurrentAlignment { get; set; } = Alignment.TopLeft;
        public int SelectedIndex { get; set; } = 0;
        public bool UseFill { get; set; } = true;
        public int ClickCount { get; set; } = 0;
    }

    private static readonly (string Label, Alignment Value)[] AlignmentOptions =
    [
        ("Top Left", Alignment.TopLeft),
        ("Top Center", Alignment.TopCenter),
        ("Top Right", Alignment.TopRight),
        ("Left Center", Alignment.LeftCenter),
        ("Center", Alignment.Center),
        ("Right Center", Alignment.RightCenter),
        ("Bottom Left", Alignment.BottomLeft),
        ("Bottom Center", Alignment.BottomCenter),
        ("Bottom Right", Alignment.BottomRight),
    ];

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating align demo example");

        var state = new AlignDemoState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.HSplitter(
                // Left panel: alignment selector
                ctx.Border(b => [
                    b.VStack(v => [
                        v.Text("Select Alignment:"),
                        v.Text(""),
                        v.List(AlignmentOptions.Select(o => o.Label).ToArray())
                            .OnSelectionChanged(e =>
                            {
                                state.SelectedIndex = e.SelectedIndex;
                                state.CurrentAlignment = AlignmentOptions[e.SelectedIndex].Value;
                            })
                    ])
                ], title: "Alignments"),
                // Right panel: preview with toggle and explanation
                ctx.VStack(v => [
                    v.HStack(h => [
                        h.Text("Fill(): "),
                        h.ToggleSwitch(["Off", "On"], state.UseFill ? 1 : 0)
                            .OnSelectionChanged(e => state.UseFill = e.SelectedIndex == 1)
                    ]),
                    v.VSplitter(
                        // Top: alignment preview
                        v.Border(b => 
                            state.UseFill
                                ? [b.Align(state.CurrentAlignment,
                                    b.Border(inner => [
                                        inner.VStack(vs => [
                                            vs.Text($"Clicks: {state.ClickCount}"),
                                            vs.Button("Click Me!")
                                                .OnClick(_ => state.ClickCount++)
                                        ])
                                    ])
                                  ).Fill()]
                                : [b.Align(state.CurrentAlignment,
                                    b.Border(inner => [
                                        inner.VStack(vs => [
                                            vs.Text($"Clicks: {state.ClickCount}"),
                                            vs.Button("Click Me!")
                                                .OnClick(_ => state.ClickCount++)
                                        ])
                                    ])
                                  )]
                        , title: $"Preview: {AlignmentOptions[state.SelectedIndex].Label}"),
                        // Bottom: explanation
                        v.Border(b => [
                            b.Text(state.UseFill
                                ? "With Fill() enabled, the Align widget expands to fill all available space. This allows vertical alignments (VCenter, Bottom) to position content anywhere within the container."
                                : "Without Fill(), the Align widget only takes as much space as its content needs. Vertical alignment has no effect since there's no extra space. Only horizontal alignment works within the available width."
                            ).Wrap()
                        ], title: "Explanation")
                    , topHeight: 10).Fill()
                ]),
                leftWidth: 22
            );
        };
    }
}
