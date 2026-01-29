using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Spinner Widget Documentation: Built-in Styles
/// Demonstrates the variety of built-in spinner styles available in Hex1b.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the stylesCode sample in:
/// src/content/guide/widgets/spinner.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class SpinnerStylesExample(ILogger<SpinnerStylesExample> logger) : Hex1bExample
{
    private readonly ILogger<SpinnerStylesExample> _logger = logger;

    public override string Id => "spinner-styles";
    public override string Title => "Spinner Widget - Styles";
    public override string Description => "Demonstrates the variety of built-in spinner styles";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating spinner styles example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Built-in Spinner Styles"),
                v.Text(""),
                v.HStack(h => [
                    h.Spinner(SpinnerStyle.Dots), h.Text(" Dots  "),
                    h.Spinner(SpinnerStyle.Line), h.Text(" Line  "),
                    h.Spinner(SpinnerStyle.Arrow), h.Text(" Arrow")
                ]),
                v.HStack(h => [
                    h.Spinner(SpinnerStyle.Circle), h.Text(" Circle  "),
                    h.Spinner(SpinnerStyle.Square), h.Text(" Square  "),
                    h.Spinner(SpinnerStyle.Bounce), h.Text(" Bounce")
                ]),
                v.Text(""),
                v.Text("Multi-Character Styles"),
                v.HStack(h => [
                    h.Spinner(SpinnerStyle.BouncingBall), h.Text(" BouncingBall  "),
                    h.Spinner(SpinnerStyle.LoadingBar), h.Text(" LoadingBar")
                ])
            ]);
        };
    }
}
