using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

public class TextBoxUnicodeExample(ILogger<TextBoxUnicodeExample> logger) : Hex1bExample
{
    private readonly ILogger<TextBoxUnicodeExample> _logger = logger;

    public override string Id => "textbox-unicode";
    public override string Title => "TextBox - Unicode Support";
    public override string Description => "TextBox with Unicode characters including emoji and CJK text.";

    private const string DefaultText = "Hello 🎉 日本語 émoji 🚀 中文 ✨";

    private class UnicodeState
    {
        public string Input { get; set; } = DefaultText;
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating textbox unicode example");
        var state = new UnicodeState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Unicode Text Editing"),
                v.Text("─────────────────────"),
                v.Text(""),
                v.TextBox(state.Input).OnTextChanged(args => state.Input = args.NewText),
                v.Text(""),
                v.Text("Try navigating with arrow keys, deleting emoji,"),
                v.Text("or adding your own Unicode characters!"),
                v.Text(""),
                v.Button("Reset to Default").OnClick(_ => state.Input = DefaultText)
            ]);
        };
    }
}
