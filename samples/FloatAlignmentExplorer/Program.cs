using Hex1b;
using Hex1b.Widgets;

string horizontal = "(none)";
string vertical = "(none)";
int hOffset = 0;
int vOffset = 0;

string[] horizontalOptions = ["(none)", "AlignLeft", "AlignRight", "ExtendLeft", "ExtendRight"];
string[] verticalOptions = ["(none)", "AlignTop", "AlignBottom", "ExtendTop", "ExtendBottom"];
string[] offsetOptions = ["0", "-2", "-1", "1", "2", "3", "4"];

var app = new Hex1bApp(ctx => ctx.VStack(v =>
{
    // Anchor widget — padded and centered so relative placement is visible
    var anchor = v.Center(
        v.Padding(8, 8, 4, 4,
            v.Border(b => [
                b.Text("  Anchor Widget  ")
            ]).Title("Anchor")
        )
    );

    // Build the float with selected alignment
    var floated = v.Float(
        v.Border(b => [
            b.Text("Float")
        ]).Title("Float")
    );

    floated = ApplyAlignment(floated, anchor);

    return [
        v.Text(" Float Alignment Explorer (Tab: navigate, Enter: open picker, ↑↓: select, Ctrl+C: quit)"),
        v.Text(""),
        v.HStack(h => [
            h.Text(" Horizontal: "),
            h.Picker(horizontalOptions).OnSelectionChanged(e => horizontal = e.SelectedText),
            h.Text("  Offset: "),
            h.Picker(offsetOptions).OnSelectionChanged(e => hOffset = int.Parse(e.SelectedText)),
        ]),
        v.HStack(h => [
            h.Text(" Vertical:   "),
            h.Picker(verticalOptions).OnSelectionChanged(e => vertical = e.SelectedText),
            h.Text("  Offset: "),
            h.Picker(offsetOptions).OnSelectionChanged(e => vOffset = int.Parse(e.SelectedText)),
        ]),
        v.Text(""),
        v.Text($" H: {horizontal} ({hOffset})  V: {vertical} ({vOffset})"),
        v.Text(""),
        anchor,
        floated,
    ];
}), new Hex1bAppOptions { EnableMouse = true });

await app.RunAsync();

FloatWidget ApplyAlignment(FloatWidget floated, Hex1bWidget anchor)
{
    floated = horizontal switch
    {
        "AlignLeft" => floated.AlignLeft(anchor, hOffset),
        "AlignRight" => floated.AlignRight(anchor, hOffset),
        "ExtendLeft" => floated.ExtendLeft(anchor, hOffset),
        "ExtendRight" => floated.ExtendRight(anchor, hOffset),
        _ => floated,
    };

    floated = vertical switch
    {
        "AlignTop" => floated.AlignTop(anchor, vOffset),
        "AlignBottom" => floated.AlignBottom(anchor, vOffset),
        "ExtendTop" => floated.ExtendTop(anchor, vOffset),
        "ExtendBottom" => floated.ExtendBottom(anchor, vOffset),
        _ => floated,
    };

    // If neither axis is set, default to a visible absolute position
    if (horizontal == "(none)" && vertical == "(none)")
    {
        floated = floated.Absolute(25, 8);
    }

    return floated;
}
