using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class TextBoxSubmitExample(ILogger<TextBoxSubmitExample> logger) : Hex1bExample
{
    private readonly ILogger<TextBoxSubmitExample> _logger = logger;

    public override string Id => "textbox-submit";
    public override string Title => "TextBox - Submit Handler";
    public override string Description => "TextBox with submit handler for form-like input.";

    private class SubmitState
    {
        public string Input { get; set; } = "";
        public List<string> Messages { get; } = [];
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating textbox submit example");
        var state = new SubmitState();

        return () =>
        {
            var ctx = new RootContext();
            
            // Build message widgets
            var messageWidgets = state.Messages
                .TakeLast(5)
                .Select(m => ctx.Text($"  • {m}"))
                .ToArray();

            return ctx.VStack(v => [
                v.Text("Chat Demo"),
                v.Text("──────────"),
                v.Text(""),
                v.Text("Type a message and press Enter:"),
                v.TextBox(state.Input)
                    .OnTextChanged(args => state.Input = args.NewText)
                    .OnSubmit(args => {
                        if (!string.IsNullOrWhiteSpace(state.Input))
                        {
                            state.Messages.Add(state.Input);
                            state.Input = "";
                        }
                    }),
                v.Text(""),
                v.Text("Messages:"),
                ..messageWidgets
            ]);
        };
    }
}
