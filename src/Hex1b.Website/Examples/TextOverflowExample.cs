using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Text Widget Documentation: Overflow Modes
/// Demonstrates TextOverflow.Wrap and TextOverflow.Ellipsis behaviors.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the overflowCode sample in:
/// src/content/guide/widgets/text.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TextOverflowExample(ILogger<TextOverflowExample> logger) : Hex1bExample
{
    private readonly ILogger<TextOverflowExample> _logger = logger;

    public override string Id => "text-overflow";
    public override string Title => "Text Widget - Overflow Modes";
    public override string Description => "Demonstrates text overflow behaviors";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating text overflow example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("═══ Text Overflow Modes ═══"),
                v.Text(""),
                v.Text("Wrap Mode:"),
                v.Text(
                    "This is a long description that demonstrates text wrapping behavior in Hex1b. " +
                    "When the text content exceeds the available width of the container, it automatically " +
                    "breaks at word boundaries to fit within the allocated space. This ensures that all " +
                    "content remains visible to the user without requiring horizontal scrolling. The widget's " +
                    "measured height increases dynamically based on the number of wrapped lines.",
                    TextOverflow.Wrap
                ),
                v.Text(""),
                v.Text("Ellipsis Mode:"),
                v.Text("This is a much longer piece of text that will definitely be truncated with an ellipsis character sequence when it exceeds the available fixed width of forty columns", TextOverflow.Ellipsis)
                    .FixedWidth(40),
                v.Text(""),
                v.Text("Default (Overflow) Mode:"),
                v.Text("This text extends beyond its allocated bounds and will be clipped by the parent container if clipping is enabled, otherwise it may render outside the expected area")
            ]);
        };
    }
}
