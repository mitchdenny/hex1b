using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// A basic responsive example showing width-based layout switching.
/// </summary>
public class ResponsiveBasicExample(ILogger<ResponsiveBasicExample> logger) : Hex1bExample
{
    private readonly ILogger<ResponsiveBasicExample> _logger = logger;

    public override string Id => "responsive-basic";
    public override string Title => "Responsive - Basic";
    public override string Description => "Basic responsive layout that changes based on terminal width.";

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating responsive basic example");

        return () =>
        {
            var ctx = new RootContext();
            return ctx.Responsive(r => [
                r.WhenMinWidth(80, r =>
                    r.Border(b => [
                        b.Text("🖥️  WIDE LAYOUT"),
                        b.Text(""),
                        b.Text("Terminal width >= 80 columns"),
                        b.Text(""),
                        b.Text("This layout appears when you"),
                        b.Text("have plenty of horizontal space."),
                        b.Text(""),
                        b.Text("Try resizing your terminal!")
                    ], title: "Wide View")
                ),
                r.Otherwise(r =>
                    r.Border(b => [
                        b.Text("📱 NARROW"),
                        b.Text(""),
                        b.Text("Width < 80"),
                        b.Text(""),
                        b.Text("Compact view"),
                        b.Text("for narrow"),
                        b.Text("terminals.")
                    ], title: "Narrow")
                )
            ]);
        };
    }
}
