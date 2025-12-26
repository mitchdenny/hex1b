using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Progress Widget Documentation: Basic Usage
/// Demonstrates simple determinate progress bars within a VStack layout.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/progress.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ProgressBasicExample(ILogger<ProgressBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ProgressBasicExample> _logger = logger;

    public override string Id => "progress-basic";
    public override string Title => "Progress Widget - Basic Usage";
    public override string Description => "Demonstrates basic progress bar display";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating progress basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Download Progress"),
                v.Progress(75),
                v.Text(""),
                v.Text("Upload Progress"),
                v.Progress(30)
            ]);
        };
    }
}
