using Hex1b;
using Hex1b.Widgets;

var state = new AlignmentState();

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.VStack(v =>
    {
        // Anchor — the inner border is the alignment target
        var anchorBorder = v.Border(b => [
            b.Text("  Anchor Widget  ")
        ]).Title("Anchor");

        // Wrap in Center + Padding so we have space around the anchor
        var anchorDisplay = v.Center(
            v.Padding(8, 8, 3, 3, anchorBorder)
        );

        var floated = v.Float(
            v.Border(b => [ b.Text("Float") ]).Title("Float")
        );

        floated = state.Horizontal switch
        {
            "AlignLeft" => floated.AlignLeft(anchorBorder, state.HOffset),
            "AlignRight" => floated.AlignRight(anchorBorder, state.HOffset),
            "ExtendLeft" => floated.ExtendLeft(anchorBorder, state.HOffset),
            "ExtendRight" => floated.ExtendRight(anchorBorder, state.HOffset),
            _ => floated,
        };
        floated = state.Vertical switch
        {
            "AlignTop" => floated.AlignTop(anchorBorder, state.VOffset),
            "AlignBottom" => floated.AlignBottom(anchorBorder, state.VOffset),
            "ExtendTop" => floated.ExtendTop(anchorBorder, state.VOffset),
            "ExtendBottom" => floated.ExtendBottom(anchorBorder, state.VOffset),
            _ => floated,
        };
        if (state.Horizontal == "(none)" && state.Vertical == "(none)")
            floated = floated.Absolute(25, 8);

        return [
            v.Text(""),
            v.HStack(h => [
                h.Text(" Horizontal: "),
                h.Picker("(none)", "AlignLeft", "AlignRight", "ExtendLeft", "ExtendRight")
                    .OnSelectionChanged(e => state.Horizontal = e.SelectedText),
                h.Text("  Offset: "),
                h.Picker("0", "-2", "-1", "1", "2", "3", "4")
                    .OnSelectionChanged(e => state.HOffset = int.Parse(e.SelectedText)),
            ]),
            v.HStack(h => [
                h.Text(" Vertical:   "),
                h.Picker("(none)", "AlignTop", "AlignBottom", "ExtendTop", "ExtendBottom")
                    .OnSelectionChanged(e => state.Vertical = e.SelectedText),
                h.Text("  Offset: "),
                h.Picker("0", "-2", "-1", "1", "2", "3", "4")
                    .OnSelectionChanged(e => state.VOffset = int.Parse(e.SelectedText)),
            ]),
            v.Text(""),
            v.Text($" H: {state.Horizontal} ({state.HOffset})  V: {state.Vertical} ({state.VOffset})"),
            v.Text(""),
            anchorDisplay,
            floated,
        ];
    }))
    .Build();

await terminal.RunAsync();

class AlignmentState
{
    public string Horizontal { get; set; } = "(none)";
    public string Vertical { get; set; } = "(none)";
    public int HOffset { get; set; }
    public int VOffset { get; set; }
}
