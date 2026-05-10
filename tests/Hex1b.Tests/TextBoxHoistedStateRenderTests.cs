using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// End-to-end render coverage for hoisted <see cref="TextBoxState"/>.
///
/// The unit-level <see cref="TextBoxWidgetTests"/> verify that the node's
/// <c>Text</c> property reflects parent mutations after reconciliation —
/// but the framework's render-skip optimisation lives downstream of that.
/// If <see cref="TextBoxWidget.ReconcileAsync"/> ever stops calling
/// <c>node.MarkDirty()</c> when the hoisted state's <c>Version</c> advances,
/// the unit tests would still pass while real apps would silently strand
/// stale text on screen.
///
/// These tests pin the <c>Version</c> + <c>MarkDirty</c> invariant against
/// the actual render pipeline by spinning up a real <see cref="Hex1bApp"/>
/// against a headless terminal, mutating hoisted state via a non-textbox
/// input gesture (button click), and asserting the rendered surface
/// reflects the new value.
/// </summary>
public class TextBoxHoistedStateRenderTests
{
    [Fact]
    public async Task HoistedState_TextMutatedFromButtonHandler_RendersOnScreen()
    {
        // ── ARRANGE ────────────────────────────────────────────────────
        // The hoisted state object that BOTH the textbox and the button
        // handler refer to. The button handler mutates state.Text out of
        // band — i.e. without going through the textbox's own input
        // handling — which is exactly the path that requires the Version
        // counter + MarkDirty plumbing in TextBoxWidget.ReconcileAsync.
        var state = new TextBoxState("alpha");

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Mutate")
                        .OnClick(_ => { state.Text = "BRAVO"; return Task.CompletedTask; }),
                    new TextBoxWidget().State(state),
                })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        // ── ACT ────────────────────────────────────────────────────────
        // 1. Wait for the initial state to render.
        // 2. Send Tab to focus the button (VStack focuses the first
        //    focusable child by default, but explicit focus management
        //    keeps this resilient to focus-routing changes).
        // 3. Press Enter to fire the button's OnClick → mutates state.Text.
        // 4. Wait for the rendered surface to show the new value.
        // 5. Capture the final frame and exit.
        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("alpha"), TimeSpan.FromSeconds(5))
            .Enter()                                            // click the focused button
            .WaitUntil(s => s.ContainsText("BRAVO"), TimeSpan.FromSeconds(5))
            .Capture("after-mutation")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        // ── ASSERT ─────────────────────────────────────────────────────
        // The new value rendered.
        Assert.True(snapshot.ContainsText("BRAVO"),
            "Hoisted state mutation from button handler should render on screen.");

        // And the old value did NOT linger. This is the stronger of the
        // two assertions: a render-skip bug would leave "alpha" on the
        // screen while the textbox reports "BRAVO" internally. Since
        // TextBoxWidget's hoisted-state branch overwrites the buffer
        // wholesale, the only way "alpha" survives is if the cells were
        // never re-painted.
        Assert.False(snapshot.ContainsText("alpha"),
            "Stale text from before the mutation should have been overwritten.");
    }

    [Fact]
    public async Task HoistedState_RepeatedMutationsAllRender()
    {
        // The Version counter is monotonic. Multiple in-flight mutations
        // between renders should each produce a fresh frame for the next
        // render pass, not get coalesced into a no-op (which would happen
        // if MarkDirty was guarded by a "did the value change since last
        // reconcile" check that cleared between mutations).
        var state = new TextBoxState("");
        int clickCount = 0;

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Bump")
                        .OnClick(_ => { state.Text = $"step-{++clickCount}"; return Task.CompletedTask; }),
                    new TextBoxWidget().State(state),
                })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Bump"), TimeSpan.FromSeconds(5))
            .Enter()
            .WaitUntil(s => s.ContainsText("step-1"), TimeSpan.FromSeconds(5))
            .Enter()
            .WaitUntil(s => s.ContainsText("step-2"), TimeSpan.FromSeconds(5))
            .Enter()
            .WaitUntil(s => s.ContainsText("step-3"), TimeSpan.FromSeconds(5))
            .Capture("after-three-mutations")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal(3, clickCount);
        Assert.True(snapshot.ContainsText("step-3"));
        Assert.False(snapshot.ContainsText("step-1"),
            "Earlier values should have been overwritten on each render.");
        Assert.False(snapshot.ContainsText("step-2"),
            "Earlier values should have been overwritten on each render.");
    }

    [Fact]
    public async Task HoistedState_UserTypingStillWorksAfterParentMutation()
    {
        // Mixing the two paths (parent mutation + user keystroke) is the
        // realistic composite use-case. Verify that after a parent
        // overwrite, the textbox is still alive and accepts input — the
        // user's keystroke must extend the parent-supplied text rather
        // than being dropped.
        var state = new TextBoxState("");

        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        using var app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(
                new VStackWidget(new Hex1bWidget[]
                {
                    new ButtonWidget("Seed")
                        .OnClick(_ =>
                        {
                            state.Text = "/help";
                            state.CursorPosition = state.Text.Length;
                            return Task.CompletedTask;
                        }),
                    new TextBoxWidget().State(state),
                })),
            new Hex1bAppOptions { WorkloadAdapter = workload });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Seed"), TimeSpan.FromSeconds(5))
            .Enter()                                            // click button → state.Text = "/help"
            .WaitUntil(s => s.ContainsText("/help"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Tab)                                  // move focus into textbox
            .Type(" me")                                        // user types " me"
            .WaitUntil(s => s.ContainsText("/help me"), TimeSpan.FromSeconds(5))
            .Capture("after-mutation-and-typing")
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyWithCaptureAsync(terminal, TestContext.Current.CancellationToken);

        await runTask;

        Assert.Equal("/help me", state.Text);
        Assert.True(snapshot.ContainsText("/help me"));
    }
}
