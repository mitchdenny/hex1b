using Custard;
using Custard.Widgets;

// Set up cancellation with Ctrl+C
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // Prevent immediate termination
    cts.Cancel();
};

// Create the textbox states (persist across renders)
var textBox1 = new TextBoxState { Text = "First textbox" };
textBox1.CursorPosition = textBox1.Text.Length;

var textBox2 = new TextBoxState { Text = "Second textbox" };
textBox2.CursorPosition = textBox2.Text.Length;

// Create and run the app
using var app = new CustardApp(ct => App(textBox1, textBox2, ct));
await app.RunAsync(cts.Token);

// The root component
static Task<CustardWidget> App(TextBoxState state1, TextBoxState state2, CancellationToken cancellationToken)
    => CustardWidgets.VStackAsync(cancellationToken,
        new HStackWidget([new TextBlockWidget("Name:  "), new TextBoxWidget(state1)]),
        new HStackWidget([new TextBlockWidget("Email: "), new TextBoxWidget(state2)])
    );