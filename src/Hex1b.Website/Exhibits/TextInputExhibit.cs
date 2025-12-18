using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Exhibits;

public class TextInputExhibit(ILogger<TextInputExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<TextInputExhibit> _logger = logger;

    public override string Id => "text-input";
    public override string Title => "Text Input";
    public override string Description => "Interactive text input using Hex1b TextBox widget.";

    /// <summary>
    /// State for this exhibit.
    /// </summary>
    private class TextInputState
    {
        public string Input { get; set; } = "";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        var state = new TextInputState();
        
        return () =>
        {
            var ctx = new RootContext();
            
            var widget = ctx.VStack(v => [
                v.Text("Interactive Text Input"),
                v.Text("─────────────────────────"),
                v.Text(""),
                v.TextBox(state.Input, args => state.Input = args.NewText),
                v.Text(""),
                v.Text("Type something! Use Backspace to delete.")
            ]);

            return widget;
        };
    }
}
