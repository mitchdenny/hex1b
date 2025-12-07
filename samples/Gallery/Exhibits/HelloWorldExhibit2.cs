using Hex1b;
using Hex1b.Fluent;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Gallery.Exhibits;

/// <summary>
/// A hello world exhibit using the new WidgetContext&lt;TParent, TState&gt; API.
/// Demonstrates the functional, return-based approach.
/// </summary>
public class HelloWorldExhibit2(ILogger<HelloWorldExhibit2> logger) : Hex1bExhibit
{
    private readonly ILogger<HelloWorldExhibit2> _logger = logger;

    public override string Id => "hello-world-v2";
    public override string Title => "Hello World (v2 API)";
    public override string Description => "Hello world using the new WidgetContext<TParent, TState> API.";

    public override string SourceCode => """
        // The new API uses WidgetCtx<TParent, TState>
        // - TParent constrains what children are valid
        // - TState allows progressive state narrowing
        // - Collection expressions for children: [v.Text("a"), v.Button("b", () => {})]
        
        public class AppState
        {
            public int ClickCount { get; set; }
        }
        
        var state = new AppState();
        var ctx = new RootCtx<AppState>(state);
        
        // Build using collection expression syntax
        var widget = ctx.VStack(v => [
            v.Text("╔════════════════════════════════════╗"),
            v.Text("║    Hello, New API World!           ║"),
            v.Text("║    Collection Expression Syntax    ║"),
            v.Text("╚════════════════════════════════════╝"),
            v.Text(""),
            v.Text(s => $"Click count: {s.ClickCount}"),
            v.Text(""),
            v.Button("Click me!", () => state.ClickCount++)
        ]);
        """;

    /// <summary>
    /// Simple state for this exhibit.
    /// </summary>
    private class HelloState
    {
        public int ClickCount { get; set; }
    }

    public override Func<CancellationToken, Task<Hex1bWidget>> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating hello world v2 widget builder");

        // Create state once, captured in closure
        var state = new HelloState();

        return ct =>
        {
            // Create root context with the state
            var ctx = new RootCtx<HelloState>(state);

            // Build using collection expression syntax
            var widget = ctx.VStack(v => [
                v.Text("╔════════════════════════════════════╗"),
                v.Text("║    Hello, New API World!           ║"),
                v.Text("║    Collection Expression Syntax    ║"),
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
