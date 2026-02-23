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
        public int Offset { get; set; }
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
                // Anchor widget
                var anchor = v.Border(b => [
                    b.Text("  Anchor Widget  ")
                ]).Title("Anchor");

                // Build the float with selected alignment
                var floated = v.Float(
                    v.Border(b => [
                        b.Text("Float")
                    ]).Title("Float")
                );

                floated = ApplyAlignment(floated, anchor, state);

                return [
                    v.Text(""),
                    v.HStack(h => [
                        h.Text(" Horizontal: "),
                        h.Picker(HorizontalOptions).OnSelectionChanged(e => state.Horizontal = e.SelectedText),
                        h.Text("  Vertical: "),
                        h.Picker(VerticalOptions).OnSelectionChanged(e => state.Vertical = e.SelectedText),
                        h.Text("  Offset: "),
                        h.Picker(OffsetOptions)
                            .OnSelectionChanged(e => state.Offset = int.Parse(e.SelectedText)),
                    ]),
                    v.Text(""),
                    v.Text($" H: {state.Horizontal}  V: {state.Vertical}  Offset: {state.Offset}"),
                    v.Text(""),
                    anchor,
                    floated,
                ];
            });
        };
    }

    private static FloatWidget ApplyAlignment(FloatWidget floated, Hex1bWidget anchor, AlignmentState state)
    {
        // Apply horizontal alignment
        floated = state.Horizontal switch
        {
            "AlignLeft" => floated.AlignLeft(anchor, state.Offset),
            "AlignRight" => floated.AlignRight(anchor, state.Offset),
            "ExtendLeft" => floated.ExtendLeft(anchor, state.Offset),
            "ExtendRight" => floated.ExtendRight(anchor, state.Offset),
            _ => floated,
        };

        // Apply vertical alignment
        floated = state.Vertical switch
        {
            "AlignTop" => floated.AlignTop(anchor, state.Offset),
            "AlignBottom" => floated.AlignBottom(anchor, state.Offset),
            "ExtendTop" => floated.ExtendTop(anchor, state.Offset),
            "ExtendBottom" => floated.ExtendBottom(anchor, state.Offset),
            _ => floated,
        };

        // If neither axis is set, default to a visible absolute position
        if (state.Horizontal == "(none)" && state.Vertical == "(none)")
        {
            floated = floated.Absolute(25, 6);
        }

        return floated;
    }
}
