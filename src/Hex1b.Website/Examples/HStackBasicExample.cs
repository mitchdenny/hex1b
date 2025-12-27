using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// HStack Widget Documentation: Basic Usage
/// Demonstrates horizontal layout with form elements.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/hstack.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class HStackBasicExample(ILogger<HStackBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<HStackBasicExample> _logger = logger;

    public override string Id => "hstack-basic";
    public override string Title => "HStack Widget - Basic Usage";
    public override string Description => "Demonstrates horizontal layout with form elements";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating HStack basic example widget builder");

        var name = "";
        var saved = false;

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.HStack(h => [
                    h.Text("Name:"),
                    h.Text("  "),
                    h.TextBox(name)
                        .OnTextChanged(args => { name = args.NewText; saved = false; })
                        .Fill(),
                    h.Text("  "),
                    h.Button("Save").OnClick(_ => saved = true)
                ]),
                v.Text(""),
                v.Text(saved ? $"Saved: {name}" : "Enter a name and click Save"),
                v.Text(""),
                v.Text("Use Tab to navigate between fields")
            ]);
        };
    }
}
