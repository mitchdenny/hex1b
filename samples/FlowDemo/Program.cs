using Hex1b;
using Hex1b.Flow;

// Hex1b Flow Demo
// Demonstrates the normal-buffer TUI model with inline slices and full-screen transitions.

Console.WriteLine("=== Hex1b Flow Demo ===");
Console.WriteLine("This demo shows inline micro-TUI slices and a full-screen TUI transition.");
Console.WriteLine();

// Capture cursor position before entering the terminal (raw mode)
var cursorRow = Console.GetCursorPosition().Top;

await Hex1bTerminal.CreateBuilder()
    .WithHex1bFlow(async flow =>
    {
        // Step 1: Inline slice — pick a color
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

        // Step 2: Full-screen TUI — detailed color viewer (enters alt-buffer)
        var fullScreenResult = "";

        await flow.FullScreenAsync((app, options) =>
        {
            return ctx => ctx.VStack(v =>
            [
                v.Text($"=== Full-Screen Color Viewer ===").FillWidth(),
                v.Separator(),
                v.Text($"You selected: {choice}"),
                v.Text(""),
                v.Text("This is a full-screen TUI running in the alternate buffer."),
                v.Text("The inline content above is preserved and will reappear when you exit."),
                v.Text(""),
                v.HStack(h =>
                [
                    h.Button($"Accept {choice}").OnClick(e =>
                    {
                        fullScreenResult = $"Accepted {choice}";
                        e.Context.RequestStop();
                    }),
                    h.Button("Go back").OnClick(e =>
                    {
                        fullScreenResult = "Went back";
                        e.Context.RequestStop();
                    }),
                ]),
            ]);
        });

        // Step 3: Inline slice — final confirmation (back in normal buffer)
        var done = false;

        await flow.SliceAsync(
            builder: ctx => ctx.VStack(v =>
            [
                v.Text($"Full-screen result: {fullScreenResult}"),
                v.Button("Finish").OnClick(e => { done = true; e.Context.RequestStop(); }),
            ]),
            @yield: ctx => ctx.Text(done ? "✓ All done!" : "✗ Aborted.")
        );
    }, options => options.InitialCursorRow = cursorRow)
    .Build()
    .RunAsync();

Console.WriteLine();
Console.WriteLine("Flow complete! Back to normal terminal.");
