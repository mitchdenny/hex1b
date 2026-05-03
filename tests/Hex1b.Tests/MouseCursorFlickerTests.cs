using Hex1b.Input;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

/// <summary>
/// Regression tests for mouse cursor flicker during animated redraws.
///
/// When mouse support is enabled and an animation drives frequent redraws
/// (for example <c>EffectPanel.RedrawAfter(50)</c> at 20 fps), each render
/// frame emits <c>?25l</c> (hide cursor) before cell writes and
/// <c>SetCursorPosition + ?25h + cursor-shape</c> after. Without atomic frame
/// commits the terminal renders the intermediate "cursor hidden" / "cursor at
/// last cell write" states, producing visible blinking of the mouse pointer.
///
/// The fix wraps the entire render frame's terminal output in DEC private mode
/// 2026 (Synchronized Update Mode). Compatible terminals buffer everything
/// between <c>?2026h</c> and <c>?2026l</c> and paint atomically; non-supporting
/// terminals ignore the sequences.
/// </summary>
public class MouseCursorFlickerTests
{
    /// <summary>
    /// Workload filter that records the order of cursor-related private-mode
    /// tokens emitted by the workload toward the terminal.
    /// </summary>
    private sealed class CursorTokenRecorder : IHex1bTerminalWorkloadFilter
    {
        private readonly object _lock = new();
        private readonly List<PrivateModeToken> _tokens = new();

        public IReadOnlyList<PrivateModeToken> Snapshot()
        {
            lock (_lock)
            {
                return _tokens.ToArray();
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _tokens.Clear();
            }
        }

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default) => default;
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) => default;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default) => default;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default) => default;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) => default;

        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        {
            lock (_lock)
            {
                for (var i = 0; i < tokens.Count; i++)
                {
                    if (tokens[i] is PrivateModeToken pm && (pm.Mode == 25 || pm.Mode == 2026))
                    {
                        _tokens.Add(pm);
                    }
                }
            }
            return default;
        }
    }

    [Fact]
    public async Task RenderFrame_WithMouseEnabled_WrapsHideShowInSynchronizedUpdate()
    {
        var recorder = new CursorTokenRecorder();
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 6)
            .AddWorkloadFilter(recorder)
            .Build();

        var counter = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new TextBlockWidget($"tick {counter}")),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        try
        {
            // Initial render and put the mouse pointer on-screen so it owns the cursor.
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("tick 0"), TimeSpan.FromSeconds(5), "initial render")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);

            await terminal.SendEventAsync(new Hex1bMouseEvent(MouseButton.None, MouseAction.Move, 12, 3, Hex1bModifiers.None));

            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.CursorX == 12 && s.CursorY == 3, TimeSpan.FromSeconds(5), "cursor at mouse position")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);

            // Reset the recorder, then drive an invalidate cycle that produces
            // a render frame.
            recorder.Reset();
            counter = 1;
            app.Invalidate();

            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("tick 1"), TimeSpan.FromSeconds(5), "after invalidate")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);

            var tokens = recorder.Snapshot();

            // The render frame must be wrapped in synchronized-update sequences.
            var begin = tokens.Select((t, i) => (t, i)).FirstOrDefault(x => x.t.Mode == 2026 && x.t.Enable);
            var end = tokens.Select((t, i) => (t, i)).LastOrDefault(x => x.t.Mode == 2026 && !x.t.Enable);

            Assert.NotNull(begin.t);
            Assert.NotNull(end.t);
            Assert.True(begin.i < end.i, "Begin sync-update token must precede end token.");

            // Every hide/show cursor token between begin and end must lie inside
            // the synchronized region — that is what makes the hide → cells →
            // show cycle invisible to the user.
            for (var i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Mode != 25) continue;
                Assert.True(
                    i > begin.i && i < end.i,
                    $"Cursor visibility token at index {i} (Enable={tokens[i].Enable}) must be inside the synchronized-update region [{begin.i}..{end.i}], not outside it.");
            }
        }
        finally
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            await runTask;
        }
    }

    [Fact]
    public async Task RenderFrame_SynchronizedUpdate_BeginAndEndAreBalanced()
    {
        var recorder = new CursorTokenRecorder();
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(40, 6)
            .AddWorkloadFilter(recorder)
            .Build();

        var counter = 0;

        using var app = new Hex1bApp(
            ctx => Task.FromResult<Hex1bWidget>(
                new TextBlockWidget($"tick {counter}")),
            new Hex1bAppOptions { WorkloadAdapter = workload, EnableMouse = true });

        var runTask = app.RunAsync(TestContext.Current.CancellationToken);

        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("tick 0"), TimeSpan.FromSeconds(5), "initial render")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);

            recorder.Reset();

            // Drive several render frames.
            for (var i = 1; i <= 3; i++)
            {
                counter = i;
                app.Invalidate();
                await new Hex1bTerminalInputSequenceBuilder()
                    .WaitUntil(s => s.ContainsText($"tick {i}"), TimeSpan.FromSeconds(5), $"frame {i}")
                    .Build()
                    .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            }

            var tokens = recorder.Snapshot();
            var beginCount = tokens.Count(t => t.Mode == 2026 && t.Enable);
            var endCount = tokens.Count(t => t.Mode == 2026 && !t.Enable);

            // Each render frame must emit a balanced begin/end pair so the
            // terminal never gets stuck buffering output indefinitely.
            Assert.Equal(beginCount, endCount);
            Assert.True(beginCount >= 3, $"Expected at least 3 sync-update frames, got {beginCount}.");
        }
        finally
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
            await runTask;
        }
    }
}
