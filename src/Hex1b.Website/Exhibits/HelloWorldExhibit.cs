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

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating hello world widget builder");

        // Create state once, captured in closure
        var state = new HelloState();

        return ct =>
        {
            // Create root context with the state
            var ctx = new RootContext<HelloState>(state);

            // Build using fluent API with collection expressions
            var widget = ctx.VStack(v => [
                v.Text("╔════════════════════════════════════╗"),
                v.Text("║    Hello, Fluent World!            ║"),
                v.Text("║    Using the Context-Based API     ║"),
                v.Text("╚════════════════════════════════════╝"),
                v.Text(""),
                v.Text(s => $"Click count: {s.ClickCount}"),
                v.Text(""),
                v.Button("Click me!", () => state.ClickCount++)
            ]);

            return Task.FromResult<Hex1bWidget>(widget);
        };
    }
}
