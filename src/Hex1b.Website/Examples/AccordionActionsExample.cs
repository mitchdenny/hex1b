using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Accordion Widget Documentation: Actions Demo
/// Demonstrates accordion sections with action icons and state feedback.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the actionsCode sample in:
/// src/content/guide/widgets/accordion.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class AccordionActionsExample(ILogger<AccordionActionsExample> logger) : Hex1bExample
{
    private readonly ILogger<AccordionActionsExample> _logger = logger;

    public override string Id => "accordion-actions";
    public override string Title => "Accordion - Action Icons";
    public override string Description => "Demonstrates accordion section header actions";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating Accordion actions example widget builder");

        var statusMessage = "Ready";

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Accordion(a => [
                    a.Section(s => [
                        s.Text("  src/"),
                        s.Text("    Program.cs"),
                        s.Text("    Utils.cs"),
                    ]).Title("EXPLORER")
                    .RightActions(ra => [
                        ra.Icon("+").OnClick(_ => statusMessage = "New file..."),
                        ra.Icon("⟳").OnClick(_ => statusMessage = "Refreshed"),
                    ]),

                    a.Section(s => [
                        s.Text("  ▸ Properties"),
                        s.Text("  ▸ Methods"),
                    ]).Title("OUTLINE")
                    .RightActions(ra => [
                        ra.Icon("⟳").OnClick(_ => statusMessage = "Outline refreshed"),
                    ]),

                    a.Section(s => [
                        s.Text("  main"),
                        s.Text("  develop"),
                    ]).Title("SOURCE CONTROL")
                    .LeftActions(la => [
                        la.Toggle("▶", "▼"),
                        la.Icon("✓").OnClick(_ => statusMessage = "Committed"),
                    ])
                    .RightActions(ra => [
                        ra.Icon("⟳").OnClick(_ => statusMessage = "Pulling..."),
                    ]),
                ]),
                v.Text(""),
                v.Text($" Status: {statusMessage}"),
            ]);
        };
    }
}
