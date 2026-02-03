using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Tree Widget Documentation: Basic Usage
/// Demonstrates basic tree creation with nested items and icons.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/tree.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class TreeBasicExample(ILogger<TreeBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<TreeBasicExample> _logger = logger;

    public override string Id => "tree-basic";
    public override string Title => "Tree Widget - Basic Usage";
    public override string Description => "Demonstrates basic tree creation with nested items and icons";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating tree basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Tree(t => [
                t.Item("Documents", docs => [
                    docs.Item("Resume.pdf").Icon("📄"),
                    docs.Item("Cover Letter.docx").Icon("📄")
                ]).Icon("📁").Expanded(),
                t.Item("Pictures", pics => [
                    pics.Item("Vacation").Icon("📁"),
                    pics.Item("Family").Icon("📁")
                ]).Icon("📸"),
                t.Item("README.md").Icon("📄")
            ]);
        };
    }
}
