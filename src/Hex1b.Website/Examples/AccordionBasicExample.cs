using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Accordion Widget Documentation: Basic Usage
/// Demonstrates a simple accordion with collapsible sections.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/accordion.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class AccordionBasicExample(ILogger<AccordionBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<AccordionBasicExample> _logger = logger;

    public override string Id => "accordion-basic";
    public override string Title => "Accordion - Basic Usage";
    public override string Description => "Demonstrates basic accordion with collapsible sections";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating Accordion basic example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Accordion(a => [
                a.Section(s => [
                    s.Text("  src/"),
                    s.Text("    Program.cs"),
                    s.Text("    Utils.cs"),
                    s.Text("    Models/"),
                ]).Title("EXPLORER"),

                a.Section(s => [
                    s.Text("  ▸ Properties"),
                    s.Text("  ▸ Methods"),
                    s.Text("  ▸ Fields"),
                ]).Title("OUTLINE"),

                a.Section(s => [
                    s.Text("  ● Updated README.md"),
                    s.Text("  ● Fixed build script"),
                ]).Title("TIMELINE"),
            ]);
        };
    }
}
