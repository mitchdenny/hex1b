using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Progress Widget Documentation: Indeterminate Mode
/// Demonstrates self-animating indeterminate progress bars.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the indeterminateCode sample in:
/// src/content/guide/widgets/progress.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ProgressIndeterminateExample(ILogger<ProgressIndeterminateExample> logger) : Hex1bExample
{
    private readonly ILogger<ProgressIndeterminateExample> _logger = logger;

    public override string Id => "progress-indeterminate";
    public override string Title => "Progress Widget - Indeterminate";
    public override string Description => "Demonstrates self-animating indeterminate progress bars";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating indeterminate progress example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Loading..."),
                v.ProgressIndeterminate()  // Self-animating!
            ]);
        };
    }
}
