using Hex1b;
using Hex1b.Input;

// Diagnostic sample for enhanced (Kitty protocol) keyboard input.
// Press/repeat details come from InputBindingActionContext.KeyEvent; holding Space
// shows whether the terminal reports releases.
var lastEvent = "None yet";
var pressAndRepeatEvents = 0;
var heldTicks = 0;
var spaceStatus = "Not pressed";

await using var terminal = Hex1bTerminal.CreateBuilder()
    .WithHex1bApp(ctx =>
    {
        return ctx.VStack(root =>
        [
            root.Text("Kitty keyboard protocol diagnostics"),
            root.Text(""),
            root.Text("Press A, S, D or an arrow key, with or without modifiers. Hold one to see repeats."),
            root.Text("Hold Space: the meter stops on release. On legacy hosts it keeps filling."),
            root.Text(""),
            root.Text($"Last event: {lastEvent}"),
            root.Text($"Press/repeat events: {pressAndRepeatEvents}"),
            root.Text($"Space: {spaceStatus}  {new string('#', Math.Min(heldTicks, 40))}"),
            root.Text(""),
            root.Text("Escape exits. Terminal and OS shortcuts may intercept some keys."),
        ]).InputBindings(bindings =>
        {
            foreach (var key in new[] { Hex1bKey.A, Hex1bKey.S, Hex1bKey.D,
                         Hex1bKey.UpArrow, Hex1bKey.DownArrow, Hex1bKey.LeftArrow, Hex1bKey.RightArrow })
            {
                Record(bindings.Key(key));
                Record(bindings.Key(key).Shift());
                Record(bindings.Key(key).Ctrl());
                Record(bindings.Key(key).Alt());
            }

            bindings.Key(Hex1bKey.Spacebar).OnHold(async (context, held) =>
            {
                spaceStatus = "Held";
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
                    spaceStatus = "Released";
                    context.Invalidate();
                }
            }, "Hold to fill the meter");

            bindings.Key(Hex1bKey.Escape).Action(context => context.RequestStop(), "Exit");

            void Record(KeyStepBuilder step) => step.Action(context =>
            {
                lastEvent = Describe(context.KeyEvent);
                pressAndRepeatEvents++;
                context.Invalidate();
            }, "Record key event");
        });
    })
    .Build();

await terminal.RunAsync();

static string Describe(Hex1bKeyEvent? input) => input is null
    ? "(no key event)"
    : $"{input.Key} {input.EventType} mods={input.Modifiers} text=\"{input.Text}\" " +
      $"code={input.UnicodeKeyCode?.ToString() ?? "-"} shifted={input.ShiftedKeyCode?.ToString() ?? "-"} " +
      $"base={input.BaseLayoutKeyCode?.ToString() ?? "-"}";
