using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// List Widget Documentation: Basic Usage
/// Demonstrates simple list creation with string items.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/list.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class ListBasicExample(ILogger<ListBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ListBasicExample> _logger = logger;

    public override string Id => "list-basic";
    public override string Title => "List Widget - Basic Usage";
    public override string Description => "Demonstrates basic list creation with string items";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating list basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Border(b => [
                b.VStack(v => [
                    v.Text("Select a fruit:"),
                    v.Text(""),
                    v.List(["Apple", "Banana", "Cherry", "Date", "Elderberry"])
                ])
            ], title: "Fruit List");
        };
    }
}
