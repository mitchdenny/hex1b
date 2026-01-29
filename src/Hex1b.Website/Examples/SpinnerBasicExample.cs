using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Spinner Widget Documentation: Basic Usage
/// Demonstrates a simple self-animating spinner with a loading message.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/spinner.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class SpinnerBasicExample(ILogger<SpinnerBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<SpinnerBasicExample> _logger = logger;

    public override string Id => "spinner-basic";
    public override string Title => "Spinner Widget - Basic Usage";
    public override string Description => "Demonstrates a simple self-animating spinner";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating spinner basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.HStack(h => [
                h.Spinner(),
                h.Text(" Loading...")
            ]);
        };
    }
}
