using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Tree Widget Documentation: Data Binding
/// Demonstrates binding a tree to a data source with typed data access.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the dataBoundCode sample in:
/// src/content/guide/widgets/tree.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TreeDataBindingExample(ILogger<TreeDataBindingExample> logger) : Hex1bExample
{
    private readonly ILogger<TreeDataBindingExample> _logger = logger;

    public override string Id => "tree-data-binding";
    public override string Title => "Tree Widget - Data Binding";
    public override string Description => "Demonstrates binding a tree to a data source with typed data access";

    private record FileNode(string Name, bool IsFolder, FileNode[] Children);

    private class ExampleState
    {
        public string ActivatedItem { get; set; } = "(none)";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating tree data binding example widget builder");

        var fileSystem = new[]
        {
            new FileNode("src", true, [
                new FileNode("Program.cs", false, []),
                new FileNode("Utils.cs", false, [])
            ]),
            new FileNode("README.md", false, [])
        };

        var state = new ExampleState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text($"Activated: {state.ActivatedItem}"),
                v.Text(""),
                v.Tree(
                    fileSystem,
                    labelSelector: f => f.Name,
                    childrenSelector: f => f.Children,
                    iconSelector: f => f.IsFolder ? "📁" : "📄"
                )
                .OnItemActivated(e =>
                {
                    var file = e.Item.GetData<FileNode>();
                    state.ActivatedItem = file.Name;
                })
            ]);
        };
    }
}
