using Hex1b;
using Hex1b.Widgets;

var selectedAlignment = Alignment.Center;

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(options => {  }, (Hex1bApp app) => ctx => ctx.HSplitter(
        // Left panel: alignment selector
        ctx.Border(b => [
            b.List(["Top Left", "Top Center", "Top Right",
                    "Left Center", "Center", "Right Center",
                    "Bottom Left", "Bottom Center", "Bottom Right"])
                .OnSelectionChanged(e => {
                    selectedAlignment = e.SelectedIndex switch {
                        0 => Alignment.TopLeft,
                        1 => Alignment.TopCenter,
                        2 => Alignment.TopRight,
                        3 => Alignment.LeftCenter,
                        4 => Alignment.Center,
                        5 => Alignment.RightCenter,
                        6 => Alignment.BottomLeft,
                        7 => Alignment.BottomCenter,
                        8 => Alignment.BottomRight,
                        _ => Alignment.Center
                    };
                })
        ]).Title("Alignments"),
        // Right panel: preview
        ctx.Border(b => [
            b.Align(selectedAlignment,
                b.Border(inner => [
                    inner.Text("Content")
                ])
            )
        ]).Title("Preview"),
        leftWidth: 22
    ))
    .Build();

await terminal.RunAsync();
