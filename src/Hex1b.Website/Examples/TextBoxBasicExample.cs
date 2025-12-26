using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class TextBoxBasicExample(ILogger<TextBoxBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<TextBoxBasicExample> _logger = logger;

    public override string Id => "textbox-basic";
    public override string Title => "TextBox - Basic Usage";
    public override string Description => "Basic text input using Hex1b TextBox widget.";

    private class TextBoxState
    {
        public string Input { get; set; } = "";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating textbox basic example");
        var state = new TextBoxState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("TextBox Widget Demo"),
                v.Text("────────────────────"),
                v.Text(""),
                v.Text("Enter your name:"),
                v.TextBox(state.Input).OnTextChanged(args => state.Input = args.NewText),
                v.Text(""),
                v.Text($"You typed: {state.Input}"),
                v.Text(""),
                v.Text("Try typing, using arrow keys, Home/End, etc.")
            ]);
        };
    }
}
