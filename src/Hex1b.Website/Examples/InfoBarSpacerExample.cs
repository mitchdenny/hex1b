using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// InfoBar Widget Documentation: Spacer
/// Demonstrates InfoBar with a spacer to push sections apart.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the spacerCode sample in:
/// src/content/guide/widgets/infobar.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class InfoBarSpacerExample(ILogger<InfoBarSpacerExample> logger) : Hex1bExample
{
    private readonly ILogger<InfoBarSpacerExample> _logger = logger;

    public override string Id => "infobar-spacer";
    public override string Title => "InfoBar - Spacer";
    public override string Description => "Demonstrates InfoBar with a spacer to push sections apart";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating InfoBar spacer example widget builder");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.VStack(v => [
                v.Border(b => [
                    b.Text("Content with a flexible status bar")
                ]).Title("Spacer Demo").FillHeight(),
                v.InfoBar(s => [
                    s.Section("Mode: INSERT"),
                    s.Spacer(),
                    s.Section("100%"),
                    s.Separator(" │ "),
                    s.Section("UTF-8")
                ])
            ]);
        };
    }
}
