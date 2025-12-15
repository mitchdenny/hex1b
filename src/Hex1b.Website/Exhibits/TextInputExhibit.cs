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
        public TextBoxState Input { get; } = new();
    }

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        var state = new TextInputState();
        
        return ct =>
        {
            var ctx = new RootContext<TextInputState>(state);
            
            var widget = ctx.VStack(v => [
                v.Text("Interactive Text Input"),
                v.Text("─────────────────────────"),
                v.Text(""),
                v.TextBox(s => s.Input),
                v.Text(""),
                v.Text("Type something! Use Backspace to delete.")
            ]);

            return Task.FromResult<Hex1bWidget>(widget);
        };
    }
}
