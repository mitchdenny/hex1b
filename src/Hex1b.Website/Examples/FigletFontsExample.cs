using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// FigletText Widget Documentation: Bundled Fonts Gallery
/// Showcases several of the built-in FIGfonts side by side.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the fontsCode sample in:
/// src/content/guide/widgets/figlet.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class FigletFontsExample(ILogger<FigletFontsExample> logger) : Hex1bExample
{
    private readonly ILogger<FigletFontsExample> _logger = logger;

    public override string Id => "figlet-fonts";
    public override string Title => "FigletText - Bundled Fonts";
    public override string Description => "Showcase of the bundled FIGfonts";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating figlet fonts example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("standard:"),
                v.FigletText("Hex1b").Font(FigletFonts.Standard),
                v.Text(""),
                v.Text("slant:"),
                v.FigletText("Hex1b").Font(FigletFonts.Slant),
                v.Text(""),
                v.Text("small:"),
                v.FigletText("Hex1b").Font(FigletFonts.Small),
            ]);
        };
    }
}
