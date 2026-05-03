using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// FigletText Widget Documentation: Basic Usage
/// Renders large ASCII-art text using the default Standard font.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/figlet.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class FigletBasicExample(ILogger<FigletBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<FigletBasicExample> _logger = logger;

    public override string Id => "figlet-basic";
    public override string Title => "FigletText - Basic Usage";
    public override string Description => "Renders large ASCII-art text using a bundled font";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating figlet basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.FigletText("Hello").Font(FigletFonts.Standard),
                v.Text(""),
                v.Text("Press Ctrl+C to exit.")
            ]);
        };
    }
}
