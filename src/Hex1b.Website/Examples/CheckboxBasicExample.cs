using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Checkbox Widget Documentation: Basic Usage
/// Demonstrates basic checkbox creation and toggle handling.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the basicCode sample in:
/// src/content/guide/widgets/checkbox.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class CheckboxBasicExample(ILogger<CheckboxBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<CheckboxBasicExample> _logger = logger;

    public override string Id => "checkbox-basic";
    public override string Title => "Checkbox Widget - Basic Usage";
    public override string Description => "Demonstrates basic checkbox creation and toggle handling";

    private class TermsState
    {
        public bool AcceptTerms { get; set; }
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating checkbox basic example widget builder");

        var state = new TermsState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Text("Terms and Conditions"),
                v.Text(""),
                v.Checkbox(state.AcceptTerms ? CheckboxState.Checked : CheckboxState.Unchecked)
                    .Label("I accept the terms")
                    .OnToggled(_ => state.AcceptTerms = !state.AcceptTerms),
                v.Text(""),
                v.Text(state.AcceptTerms ? "✓ Terms accepted" : "Please accept terms to continue")
            ]);
        };
    }
}
