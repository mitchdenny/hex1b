using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Text Widget Documentation: Basic Usage
/// Demonstrates simple text display within a VStack layout.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/text.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TextBasicExample(ILogger<TextBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<TextBasicExample> _logger = logger;

    public override string Id => "text-basic";
    public override string Title => "Text Widget - Basic Usage";
    public override string Description => "Demonstrates basic text display";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating text basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Welcome to Hex1b"),
                v.Text("Build beautiful terminal UIs")
            ]);
        };
    }
}
