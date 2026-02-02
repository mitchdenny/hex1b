using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Split Button Documentation: Basic Usage
/// Demonstrates split button with primary and secondary actions.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/split-button.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class SplitButtonBasicExample(ILogger<SplitButtonBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<SplitButtonBasicExample> _logger = logger;

    public override string Id => "split-button-basic";
    public override string Title => "Split Button - Basic Usage";
    public override string Description => "Demonstrates split button with primary and secondary actions";

    private class EditorState
    {
        public string LastAction { get; set; } = "(none)";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating split button basic example widget builder");

        var state = new EditorState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Split Button Demo"),
                v.Text(""),
                v.Text($"Last action: {state.LastAction}"),
                v.Text(""),
                v.SplitButton("Save")
                   .OnPrimaryClick(_ => state.LastAction = "Saved file")
                   .WithSecondaryAction("Save As...", _ => state.LastAction = "Save As dialog")
                   .WithSecondaryAction("Save All", _ => state.LastAction = "Saved all files")
                   .WithSecondaryAction("Save Copy", _ => state.LastAction = "Saved copy"),
                v.Text(""),
                v.Text("Click the button or press ▼ for more options")
            ]);
        };
    }
}
