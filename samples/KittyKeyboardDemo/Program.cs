using Hex1b;
using Hex1b.Input;

var lastKey = "None yet";
var keyEvents = 0;
var heldTicks = 0;
var holdStatus = "Release";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(ctx =>
    {
        return ctx.VStack(root =>
        [
            root.Text("Kitty keyboard protocol · Hex1bApp"),
            root.Text(""),
            root.Text("Hex1bApp negotiates enhanced keyboard input when the host supports it."),
            root.Text("Try A, S, or D; key repeat adds more events to the counter."),
            root.Text("Hold Space to animate the hold meter. Releasing it stops the callback."),
            root.Text(""),
            root.Text($"Last key: {lastKey}"),
            root.Text($"Press/repeat callbacks: {keyEvents}"),
            root.Text($"Space: {holdStatus}"),
            root.Text($"Held time: {new string('█', Math.Min(heldTicks, 24))}"),
            root.Text(""),
            root.Text("Escape exits. Host and OS shortcuts may intercept some keys."),
        ]).InputBindings(bindings =>
        {
            Register(Hex1bKey.A, "A");
            Register(Hex1bKey.S, "S");
            Register(Hex1bKey.D, "D");

            bindings.Key(Hex1bKey.Spacebar).OnHold(async (context, held) =>
            {
                holdStatus = "Held";
                heldTicks = 0;
                context.Invalidate();
                try
                {
                    while (true)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100), held);
                        heldTicks++;
                        context.Invalidate();
                    }
                }
                finally
                {
                    holdStatus = "Released";
                    context.Invalidate();
                }
            }, "Hold to animate");

            bindings.Key(Hex1bKey.Escape).Action(context => context.RequestStop(), "Exit");

            void Register(Hex1bKey key, string label)
                => bindings.Key(key).Action(context =>
                {
                    lastKey = label;
                    keyEvents++;
                    context.Invalidate();
                }, $"Record {label}");
        });
    })
    .Build();

await terminal.RunAsync();
