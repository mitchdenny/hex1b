using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Regression tests for issue #297 — when a focused TextBox uses the native terminal
/// cursor and a sibling widget repaints in the same frame, the hardware cursor must
/// still be restored to the TextBox even though the TextBox's own cursor coordinates
/// did not change since the previous frame.
/// </summary>
public class TextBoxNativeCursorRestoreTests
{
    [Fact]
    public async Task FocusedTextBox_AfterSiblingRepaint_NativeCursorReturnsToTextBox()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 6)
            .Build();

        // Activity text is mutated between frames. Both values are the same width
        // so the only cell change between frames is on the activity row — isolating
        // the cursor-restoration behavior from any layout shift.
        var activity = "Loading...";

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new TextBlockWidget(activity),
                    new TextBoxWidget("")
                })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // Initial render: TextBox is focused (only focusable in tree) and has
        // emitted its native bar cursor.
        var snapshot1 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Loading..."), TimeSpan.FromSeconds(5), "initial render")
            .Capture("snapshot1")
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        // Sanity: the cursor should be on the TextBox's row (row 1 of the VStack,
        // i.e. the second visible row), not on the activity row (row 0).
        // The exact column depends on the default theme's left bracket width but
        // the row should be 1.
        Assert.Equal(1, snapshot1.CursorY);

        // Mutate the activity row (same width) and force a re-render.
        activity = "Loaded....";
        app.Invalidate();

        var snapshot2 = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Loaded...."), TimeSpan.FromSeconds(5), "after activity mutation")
            .Capture("snapshot2")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // The TextBox cursor coordinates have not changed between frames, so
        // RenderCursor()'s early-return optimization kicks in. The bug is that
        // RenderFrameWithSurface() rewrote cells on the activity row (row 0),
        // moving the hardware cursor there. Without the fix, snapshot2.CursorY
        // is 0 (the activity row); with the fix it's still 1 (the TextBox row).
        Assert.Equal(snapshot1.CursorY, snapshot2.CursorY);
        Assert.Equal(snapshot1.CursorX, snapshot2.CursorX);
    }
}
