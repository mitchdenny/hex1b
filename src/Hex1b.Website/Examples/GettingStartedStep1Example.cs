using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Getting Started Step 1: Hello World
/// A simple "Hello World" example to demonstrate the basics.
/// </summary>
public class GettingStartedStep1Example(ILogger<GettingStartedStep1Example> logger) : Hex1bExample
{
    private readonly ILogger<GettingStartedStep1Example> _logger = logger;

    public override string Id => "getting-started-step1";
    public override string Title => "Getting Started - Step 1: Hello World";
    public override string Description => "A simple Hello World example";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating getting started step 1 widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Text("Hello, Hex1b!");
        };
    }
}
