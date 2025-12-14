using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

// Simple mouse tracking demonstration
var mouseX = -1;
var mouseY = -1;
var clickCount = 0;

using var cts = new CancellationTokenSource();

using var app = new Hex1bApp<object>(
    state: new object(),
    builder: ctx =>
    {
        return new BorderWidget(
            new VStackWidget([
                new TextBlockWidget("🖱️  Mouse Tracking Demo"),
                new TextBlockWidget(""),
                new TextBlockWidget($"Mouse Position: ({mouseX}, {mouseY})"),
                new TextBlockWidget($"Click Count: {clickCount}"),
                new TextBlockWidget(""),
                new TextBlockWidget("Move your mouse around!"),
                new TextBlockWidget("The yellow cursor shows the mouse position."),
                new TextBlockWidget(""),
                new ButtonWidget("Button 1", () => { }),
                new ButtonWidget("Button 2", () => { }),
                new ButtonWidget("Button 3", () => { }),
                new TextBlockWidget(""),
                new ButtonWidget("Quit (q)", () => cts.Cancel())
            ]),
            "Mouse Test"
        ).WithInputBindings(b =>
        {
            b.Key(Hex1bKey.Q).Action(() => cts.Cancel(), "Quit");
        });
    },
    options: new Hex1bAppOptions
    {
        EnableMouse = true
    }
);

Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
await app.RunAsync(cts.Token);
