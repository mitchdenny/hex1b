using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Exhibits;

/// <summary>
/// A simple hello world using the fluent context-based API.
/// </summary>
public class HelloWorldExhibit(ILogger<HelloWorldExhibit> logger) : Hex1bExhibit
{
    private readonly ILogger<HelloWorldExhibit> _logger = logger;

    public override string Id => "hello-world";
    public override string Title => "Hello World";
    public override string Description => "A simple hello world with interactive button.";

    /// <summary>
    /// Simple state for this exhibit.
    /// </summary>
    private class HelloState
    {
        public int ClickCount { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating hello world widget builder");

        // Create state once, captured in closure
        var state = new HelloState();

        return () =>
        {
            // Create root context
            var ctx = new RootContext();

            // Build using fluent API with collection expressions
            var widget = ctx.VStack(v => [
                v.Text("╔════════════════════════════════════╗"),
                v.Text("║    Hello, Fluent World!            ║"),
                v.Text("║    Using the Context-Based API     ║"),
                v.Text("╚════════════════════════════════════╝"),
                v.Text(""),
                v.Text($"Click count: {state.ClickCount}"),
                v.Text(""),
                v.Button("Click me!", _ => state.ClickCount++)
            ]);

            return widget;
        };
    }
}
