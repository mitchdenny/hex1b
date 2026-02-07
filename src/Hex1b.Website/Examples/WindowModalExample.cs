using Hex1b;
using Hex1b.Widgets;
using Microsoft.Extensions.Logging;

namespace Hex1b.Website.Examples;

/// <summary>
/// Window Widget Documentation: Modal Dialogs
/// Demonstrates modal windows with result handling.
/// </summary>
/// <remarks>
/// MIRROR WARNING: This example must stay in sync with the modalCode sample in:
/// src/content/guide/widgets/windows.md
/// When updating code here, update the corresponding markdown and vice versa.
/// </remarks>
public class WindowModalExample(ILogger<WindowModalExample> logger) : Hex1bExample
{
    private readonly ILogger<WindowModalExample> _logger = logger;

    public override string Id => "window-modal";
    public override string Title => "Modal Dialog with Result";
    public override string Description => "Demonstrates modal windows with typed result handling";

    private class ModalState
    {
        public string LastResult { get; set; } = "No dialog shown yet";
    }

    public override Func<Hex1bWidget> CreateWidgetBuilder()
    {
        _logger.LogInformation("Creating window modal example widget builder");

        var state = new ModalState();

        return () =>
        {
            var ctx = new RootContext();
            return ctx.ZStack(z => [
                z.WindowPanel()
                    .Background(b => b.VStack(v => [
                        v.Text(""),
                        v.Text($"  Last Result: {state.LastResult}"),
                        v.Text(""),
                        v.Button("Confirm Delete").OnClick(e =>
                        {
                            e.Windows.Window(w => w.VStack(v => [
                                v.Text(""),
                                v.Text("  🗑️  Delete this item?"),
                                v.Text("  This action cannot be undone."),
                                v.Text(""),
                                v.HStack(h => [
                                    h.Text("  "),
                                    h.Button("Delete").OnClick(_ => w.Window.CloseWithResult(true)),
                                    h.Text(" "),
                                    h.Button("Cancel").OnClick(_ => w.Window.CloseWithResult(false))
                                ])
                            ]))
                            .Title("Confirm Delete")
                            .Size(40, 9)
                            .Modal()
                            .OnResult<bool>(result =>
                            {
                                if (result.IsCancelled || !result.Value)
                                    state.LastResult = "Cancelled";
                                else
                                    state.LastResult = "Deleted!";
                            })
                            .Open(e.Windows);
                        })
                    ]))
                    .Fill()
            ]);
        };
    }
}
