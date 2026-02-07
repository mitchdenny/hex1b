using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Window Widget Documentation: Custom Title Bar Actions
/// Demonstrates adding custom action buttons to window title bars.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the titleActionsCode sample in:
/// src/content/guide/widgets/windows.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class WindowTitleActionsExample(ILogger<WindowTitleActionsExample> logger) : Hex1bExample
{
    private readonly ILogger<WindowTitleActionsExample> _logger = logger;

    public override string Id => "window-title-actions";
    public override string Title => "Custom Title Bar Actions";
    public override string Description => "Demonstrates custom title bar action buttons";

    private class ActionState
    {
        public bool Pinned { get; set; }
        public string LastAction { get; set; } = "";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating window title actions example widget builder");

        var state = new ActionState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
                z.WindowPanel()
                    .Background(b => b.VStack(v => [
                        v.Text(""),
                        string.IsNullOrEmpty(state.LastAction)
                            ? v.Text("  Click the button below to open a window")
                            : v.Text($"  Last action: {state.LastAction}"),
                        v.Text(""),
                        v.Button("Open Window with Actions").OnClick(e =>
                        {
                            e.Windows.Window(w => w.VStack(v => [
                                v.Text(""),
                                v.Text(state.Pinned
                                    ? "  📌 This window is pinned!"
                                    : "  Click the icons in the title bar"),
                                v.Text("")
                            ]))
                            .Title("Custom Actions")
                            .Size(42, 7)
                            .LeftTitleActions(t => [
                                t.Action("📌", _ =>
                                {
                                    state.Pinned = !state.Pinned;
                                    state.LastAction = state.Pinned ? "Pinned!" : "Unpinned";
                                }),
                                t.Action("📋", _ => state.LastAction = "Copied to clipboard")
                            ])
                            .RightTitleActions(t => [
                                t.Action("?", _ => state.LastAction = "Help requested"),
                                t.Close()
                            ])
                            .Open(e.Windows);
                        })
                    ]))
                    .Fill()
            ]);
        };
    }
}
