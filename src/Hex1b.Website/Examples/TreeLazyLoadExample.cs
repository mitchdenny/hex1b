using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Tree Widget Documentation: Lazy Loading
/// Demonstrates async lazy loading of children with spinner animation.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the lazyLoadCode sample in:
/// src/content/guide/widgets/tree.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TreeLazyLoadExample(ILogger<TreeLazyLoadExample> logger) : Hex1bExample
{
    private readonly ILogger<TreeLazyLoadExample> _logger = logger;

    public override string Id => "tree-lazy-load";
    public override string Title => "Tree Widget - Lazy Loading";
    public override string Description => "Demonstrates async lazy loading of children with spinner animation";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating tree lazy load example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Tree(t => [
                t.Item("Server 1").Icon("🖥️")
                    .OnExpanding(async _ =>
                    {
                        await Task.Delay(1000); // Simulate network call
                        var childCtx = new TreeContext();
                        return new[]
                        {
                            childCtx.Item("Database").Icon("🗃️"),
                            childCtx.Item("Cache").Icon("💾")
                        };
                    }),
                t.Item("Server 2").Icon("🖥️")
                    .OnExpanding(async _ =>
                    {
                        await Task.Delay(500);
                        var childCtx = new TreeContext();
                        return new[]
                        {
                            childCtx.Item("API Gateway").Icon("🌐")
                        };
                    })
            ]);
        };
    }
}
