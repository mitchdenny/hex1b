using Hex1b;
using Hex1b.Flow;

// Hex1b Flow Demo
// Demonstrates the normal-buffer TUI model with inline micro-TUI slices.

Console.WriteLine("=== Hex1b Flow Demo ===");
Console.WriteLine("This demo shows inline micro-TUI slices in the normal terminal buffer.");
Console.WriteLine();

// Capture cursor position before entering the terminal (raw mode)
var cursorRow = Console.GetCursorPosition().Top;

await Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        // Step 1: A simple inline slice with buttons
        var choice = "";

        await flow.SliceAsync(
            builder: ctx => ctx.VStack(v =>
            [
                v.Text("Pick your favorite color:"),
                v.Button("Red").OnClick(e => { choice = "Red"; e.Context.RequestStop(); }),
                v.Button("Green").OnClick(e => { choice = "Green"; e.Context.RequestStop(); }),
                v.Button("Blue").OnClick(e => { choice = "Blue"; e.Context.RequestStop(); }),
            ]),
            @yield: ctx => ctx.Text($"✓ You picked: {choice}")
        );

        // Step 2: Another inline slice using the previous result
        var confirmed = false;

        await flow.SliceAsync(
            builder: ctx => ctx.VStack(v =>
            [
                v.Text($"You chose {choice}. Confirm?"),
                v.HStack(h =>
                [
                    h.Button("Confirm").OnClick(e => { confirmed = true; e.Context.RequestStop(); }),
                    h.Button("Cancel").OnClick(e => { confirmed = false; e.Context.RequestStop(); }),
                ]),
            ]),
            @yield: ctx => ctx.Text(confirmed ? "✓ Confirmed!" : "✗ Cancelled.")
        );
    }, options => options.InitialCursorRow = cursorRow)
    .Build()
    .RunAsync();

Console.WriteLine();
Console.WriteLine("Flow complete! Back to normal terminal.");
