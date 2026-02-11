using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// InfoBar Widget Documentation: Widget Content
/// Demonstrates InfoBar sections containing widgets like Spinner.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the spinnerCode sample in:
/// src/content/guide/widgets/infobar.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class InfoBarSpinnerExample(ILogger<InfoBarSpinnerExample> logger) : Hex1bExample
{
    private readonly ILogger<InfoBarSpinnerExample> _logger = logger;

    public override string Id => "infobar-spinner";
    public override string Title => "InfoBar - Widget Content";
    public override string Description => "Demonstrates InfoBar sections containing widgets like Spinner";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating InfoBar spinner example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(b => [
                    b.Text("Background operation in progress...")
                ]).Title("Activity Indicator").FillHeight(),
                v.InfoBar(s => [
                    s.Section(x => x.HStack(h => [
                        h.Spinner(SpinnerStyle.Dots),
                        h.Text(" Saving...")
                    ])),
                    s.Spacer(),
                    s.Section("Ready")
                ])
            ]);
        };
    }
}
