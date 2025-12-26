using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Text Widget Documentation: Complete Example
/// Demonstrates various text features including overflow modes and size hints.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the completeCode sample in:
/// src/content/guide/widgets/text.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TextCompleteExample(ILogger<TextCompleteExample> logger) : Hex1bExample
{
    private readonly ILogger<TextCompleteExample> _logger = logger;

    public override string Id => "text-complete";
    public override string Title => "Text Widget - Complete Example";
    public override string Description => "Demonstrates various text features";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating text complete example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("═══ Application Title ═══"),
                v.Text(""),
                v.Text(
                    "This is a long description that demonstrates text wrapping. " +
                    "When the text exceeds the available width, it automatically " +
                    "breaks at word boundaries to fit within the container.",
                    TextOverflow.Wrap
                ),
                v.Text(""),
                v.Text("Status: Loading...").FillWidth(),
                v.Text("Item name that might be too long", TextOverflow.Ellipsis)
                    .FixedWidth(25)
            ]);
        };
    }
}
