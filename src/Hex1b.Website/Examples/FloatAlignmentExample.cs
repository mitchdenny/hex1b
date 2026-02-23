using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Float Widget Documentation: Alignment Explorer
/// Interactive demo showing how horizontal and vertical alignment options
/// affect the placement of a floated widget relative to an anchor.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the alignmentCode sample in:
/// src/content/guide/widgets/float.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class FloatAlignmentExample(ILogger<FloatAlignmentExample> logger) : Hex1bExample
{
    private readonly ILogger<FloatAlignmentExample> _logger = logger;

    public override string Id => "float-alignment";
    public override string Title => "Float - Alignment Explorer";
    public override string Description => "Explore how alignment options position a floated widget relative to an anchor";

    private class AlignmentState
    {
        public string Horizontal { get; set; } = "(none)";
        public string Vertical { get; set; } = "(none)";
        public int HOffset { get; set; }
        public int VOffset { get; set; }
    }

    private static readonly string[] HorizontalOptions =
        ["(none)", "AlignLeft", "AlignRight", "ExtendLeft", "ExtendRight"];

    private static readonly string[] VerticalOptions =
        ["(none)", "AlignTop", "AlignBottom", "ExtendTop", "ExtendBottom"];

    private static readonly string[] OffsetOptions =
        ["0", "-2", "-1", "1", "2", "3", "4"];

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating float alignment example widget builder");

        var state = new AlignmentState();

        return () =>
        {
            var ctx = new RootContext();

            return ctx.VStack(v =>
            {
                // Anchor — the inner border is the alignment target
                var anchorBorder = v.Border(b => [
                    b.Text("  Anchor Widget  ")
                ]).Title("Anchor");

                // Wrap in Center + Padding for visual space
                var anchorDisplay = v.Center(
                    v.Padding(8, 8, 3, 3, anchorBorder)
                );

                // Build the float with selected alignment
                var floated = v.Float(
                    v.Border(b => [
                        b.Text("Float")
                    ]).Title("Float")
                );

                floated = ApplyAlignment(floated, anchorBorder, state);

                return [
                    v.Text(""),
                    v.HStack(h => [
                        h.Text(" Horizontal: "),
                        h.Picker(HorizontalOptions).OnSelectionChanged(e => state.Horizontal = e.SelectedText),
                        h.Text("  Offset: "),
                        h.Picker(OffsetOptions).OnSelectionChanged(e => state.HOffset = int.Parse(e.SelectedText)),
                    ]),
                    v.HStack(h => [
                        h.Text(" Vertical:   "),
                        h.Picker(VerticalOptions).OnSelectionChanged(e => state.Vertical = e.SelectedText),
                        h.Text("  Offset: "),
                        h.Picker(OffsetOptions).OnSelectionChanged(e => state.VOffset = int.Parse(e.SelectedText)),
                    ]),
                    v.Text(""),
                    v.Text($" H: {state.Horizontal} ({state.HOffset})  V: {state.Vertical} ({state.VOffset})"),
                    v.Text(""),
                    anchorDisplay,
                    floated,
                ];
            });
        };
    }

    private static FloatWidget ApplyAlignment(FloatWidget floated, Hex1bWidget anchor, AlignmentState state)
    {
        floated = state.Horizontal switch
        {
            "AlignLeft" => floated.AlignLeft(anchor, state.HOffset),
            "AlignRight" => floated.AlignRight(anchor, state.HOffset),
            "ExtendLeft" => floated.ExtendLeft(anchor, state.HOffset),
            "ExtendRight" => floated.ExtendRight(anchor, state.HOffset),
            _ => floated,
        };

        floated = state.Vertical switch
        {
            "AlignTop" => floated.AlignTop(anchor, state.VOffset),
            "AlignBottom" => floated.AlignBottom(anchor, state.VOffset),
            "ExtendTop" => floated.ExtendTop(anchor, state.VOffset),
            "ExtendBottom" => floated.ExtendBottom(anchor, state.VOffset),
            _ => floated,
        };

        if (state.Horizontal == "(none)" && state.Vertical == "(none)")
        {
            floated = floated.Absolute(25, 8);
        }

        return floated;
    }
}
