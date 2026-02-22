using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// FloatPanel Widget Documentation: Interactive Overlay
/// Demonstrates FloatPanel as a HUD overlay with buttons.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the overlayCode sample in:
/// src/content/guide/widgets/float-panel.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class FloatPanelOverlayExample(ILogger<FloatPanelOverlayExample> logger) : Hex1bExample
{
    private readonly ILogger<FloatPanelOverlayExample> _logger = logger;

    public override string Id => "float-panel-overlay";
    public override string Title => "FloatPanel - Interactive Overlay";
    public override string Description => "Demonstrates FloatPanel as a HUD overlay with interactive buttons";

    private class OverlayState
    {
        public int Score { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating float panel overlay example widget builder");

        var state = new OverlayState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
                // Background content
                z.VStack(v => [
                    v.Text("═══════════════════════════════════════"),
                    v.Text("         Main Application Area         "),
                    v.Text("═══════════════════════════════════════"),
                    v.Text(""),
                    v.Text("  Content goes here..."),
                ]),
                // FloatPanel overlay with score and controls
                z.FloatPanel(f => [
                    f.Place(2, 0, f.Text($"Score: {state.Score}")),
                    f.Place(2, 8, f.Button("+1 Point").OnClick(_ => state.Score++)),
                    f.Place(20, 8, f.Button("Reset").OnClick(_ => state.Score = 0)),
                ]),
            ]);
        };
    }
}
