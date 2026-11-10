using System.Threading.Channels;
using Hex1b.Animation;
using Hex1b.Input;
using Hex1b.Widgets;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class Hex1bAppSchedulingTests
{
    [TestMethod]
    public async Task RunAsync_Spinner_UsesAppClockForVisualFrames()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var clock = new FrameTimeProvider();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => options.FrameTimeProvider = clock,
                _ => new SpinnerWidget { Style = new SpinnerStyle(TimeSpan.FromSeconds(1), "A", "B") })
            .WithHeadless()
            .WithDimensions(4, 2)
            .Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            foreach (var expected in new[] { "A", "B", "A" })
            {
                using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
                    .WaitUntil(s => s.ContainsText(expected), TimeSpan.FromSeconds(5), $"spinner frame {expected}")
                    .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
                var delay = await clock.Changes.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(TimeSpan.FromSeconds(1), delay);
                clock.Advance(delay);
            }
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task RunAsync_AnimationAndInvalidation_ShareClockAndFrameBudget()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var clock = new FrameTimeProvider();
        var state = new object();
        var values = Channel.CreateUnbounded<double>();
        AnimationTimer? timers = null;
        Hex1bApp? app = null;
        var value = 0.0;
        var builds = 0;
        var observer = new TestWidget().OnRender(_ => values.Writer.TryWrite(value));
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options =>
                {
                    options.FrameRateLimitMs = 100;
                    options.FrameTimeProvider = clock;
                    options.EnableRescue = false;
                },
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([
                        new TextBlockWidget($"Frame {++builds}"),
                        new StatePanelWidget(state, context =>
                        {
                            var animator = context.GetAnimations().Get<NumericAnimator<double>>("value", a =>
                            {
                                a.From = 0;
                                a.To = 1;
                                a.Duration = TimeSpan.FromMilliseconds(200);
                            });
                            value = animator.Value;
                            return new TextBlockWidget($"Value {value}");
                        }),
                        new AnimationProbeWidget(context => timers = context.AnimationTimer),
                        observer,
                    ]);
                })
            .WithHeadless()
            .WithDimensions(24, 6)
            .Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            Assert.AreEqual(0.0, await values.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            for (var frame = 1; frame <= 2; frame++)
            {
                var animationDelay = await clock.Changes.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(TimeSpan.FromMilliseconds(16), animationDelay);
                Assert.AreEqual(1, timers!.PendingRedrawCount);
                clock.Advance(animationDelay);
                var remaining = await clock.Delays.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(TimeSpan.FromMilliseconds(84), remaining);
                for (var i = 0; i < 32; i++)
                    app!.Invalidate();
                clock.Advance(remaining);
                Assert.AreEqual(frame * 0.5, await values.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            }
            Assert.AreEqual(0, timers!.PendingRedrawCount, "Completed animations must not leave redraw timers behind.");
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_InputDuringContinuousInvalidation_PreservesOrderAndResize(bool coalesceInput)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var presentation = new HeadlessPresentationAdapter(24, 6);
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? workload = null;
        var input = "";
        var frames = new List<(int Number, string Input, int Width, int Height)>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TestWidget().OnRender(args =>
        {
            frames.Add((args.RenderCount, input, args.Node.Parent!.Bounds.Width, args.Node.Parent.Bounds.Height));

            // The producer remains active until shutdown, including while input is queued.
            for (var i = 0; i < 32; i++)
                app!.Invalidate();

            if (args.RenderCount < 3)
                workload!.SendKey(Hex1bKey.W);
            if (args.RenderCount == 3)
            {
                workload!.SendKey(Hex1bKey.A);
                presentation.TriggerResize(30, 7);
                workload.SendKey(Hex1bKey.B);
            }
            if (input == "AB")
                finished.TrySetResult();
        });
        var builds = 0;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options =>
                {
                    workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter);
                    options.EnableInputCoalescing = coalesceInput;
                    options.EnableRescue = false;
                },
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([
                        new TextBlockWidget($"Frame {++builds}: {input}"),
                        new ButtonWidget("Focus"),
                        observer,
                    ]).InputBindings(bindings =>
                    {
                        bindings.Key(Hex1bKey.A).Action(_ => input += "A");
                        bindings.Key(Hex1bKey.B).Action(_ => input += "B");
                    });
                })
            .WithPresentation(presentation)
            .WithDimensions(24, 6)
            .Build();

        var runTask = Task.Run(() => terminal.RunAsync(cancellation.Token), cancellation.Token);
        try
        {
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("AB") && s.Width == 30 && s.Height == 7,
                    TimeSpan.FromSeconds(5), "input and resize progressed while invalidations continued")
                .Build()
                .ApplyAsync(terminal, TestContext.Current.CancellationToken);
        }
        finally
        {
            cancellation.Cancel();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.AreEqual("AB", input, "Queued keys must be handled in order, exactly once.");
        Assert.AreEqual(coalesceInput ? "AB" : "A", frames.Single(f => f.Number == 4).Input);
        Assert.AreEqual("AB", frames.Single(f => f.Number == (coalesceInput ? 4 : 6)).Input);
        Assert.AreEqual((30, 7), (frames[^1].Width, frames[^1].Height));
    }

    [TestMethod]
    [DataRow(16, 0)]
    [DataRow(16, 10)]
    [DataRow(100, 0)]
    [DataRow(100, 10)]
    public async Task RunAsync_ContinuousInvalidation_RespectsFrameRateLimit(int frameRateLimitMs, int renderDurationMs)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var clock = new FrameTimeProvider();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var interval = TimeSpan.FromMilliseconds(frameRateLimitMs);
        var renderDuration = TimeSpan.FromMilliseconds(renderDurationMs);
        Hex1bApp? app = null;
        var timestamps = new List<long>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TestWidget().OnRender(args =>
        {
            timestamps.Add(clock.GetTimestamp());
            clock.Advance(renderDuration);
            for (var i = 0; i < 32; i++)
                app!.Invalidate();
            if (args.RenderCount == 6)
                finished.TrySetResult();
        });
        var builds = 0;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options =>
                {
                    options.FrameRateLimitMs = frameRateLimitMs;
                    options.FrameTimeProvider = clock;
                    options.EnableRescue = false;
                },
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([new TextBlockWidget($"Frame {++builds}"), observer]);
                })
            .WithHeadless()
            .WithDimensions(24, 6)
            .Build();

        var runTask = Task.Run(() => terminal.RunAsync(cancellation.Token), cancellation.Token);
        try
        {
            // Wait until pacing actually schedules its delay before advancing time.
            // Startup counts as a frame, and rendering consumes part of each budget.
            for (var i = 1; i < 6; i++)
            {
                var delay = await clock.Delays.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.AreEqual(interval - renderDuration, delay, $"Frame {i + 1} must wait for the remaining budget.");
                clock.Advance(delay);
            }
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            cancellation.Cancel();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        for (var i = 1; i < 6; i++)
        {
            Assert.AreEqual(interval, clock.GetElapsedTime(timestamps[i - 1], timestamps[i]),
                $"Frames {i} and {i + 1} must be one budget apart.");
        }
    }

    [TestMethod]
    public async Task RunAsync_InputWhileFrameIsPaced_RendersWithoutAdvancingClock()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var clock = new FrameTimeProvider();
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? workload = null;
        var input = "";
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TestWidget().OnRender(_ =>
        {
            app!.Invalidate();
            if (input == "A")
                rendered.TrySetResult();
        });
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options =>
                {
                    options.FrameRateLimitMs = 1000;
                    options.FrameTimeProvider = clock;
                    options.EnableInputCoalescing = false;
                    workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter);
                },
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([new TextBlockWidget(input), new ButtonWidget("Focus"), observer])
                        .InputBindings(bindings => bindings.Key(Hex1bKey.A).Action(_ => input += "A"));
                })
            .WithHeadless()
            .WithDimensions(24, 6)
            .Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            var delay = await clock.Delays.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(TimeSpan.FromSeconds(1), delay);
            workload!.SendKey(Hex1bKey.A);
            await rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0L, clock.GetTimestamp(), "Input must not wait for the background frame deadline.");
        }
        finally
        {
            app!.RequestStop();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_ShutdownWhileFrameIsPaced_DoesNotWaitForDeadline(bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var clock = new FrameTimeProvider();
        Hex1bApp? app = null;
        var observer = new TestWidget().OnRender(_ => app!.Invalidate());
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options =>
                {
                    options.FrameRateLimitMs = 1000;
                    options.FrameTimeProvider = clock;
                },
                instance =>
                {
                    app = instance;
                    return _ => observer;
                })
            .WithHeadless()
            .Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            await clock.Delays.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel)
                cancellation.Cancel();
            else
                app!.RequestStop();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0L, clock.GetTimestamp());
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    public async Task RunAsync_AfterCancellation_CanRestartAndWaitForAnotherFrame()
    {
        var clock = new FrameTimeProvider();
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(24, 6)
            .Build();
        Hex1bApp? instance = null;
        var builds = 0;
        using var app = new Hex1bApp(
            _ =>
            {
                instance!.Invalidate();
                return Task.FromResult<Hex1bWidget>(new TextBlockWidget($"Frame {++builds}"));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = workload,
                FrameTimeProvider = clock,
                FrameRateLimitMs = 100,
                EnableRescue = false,
            });
        instance = app;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var run = app.RunAsync(cancellation.Token);
            try
            {
                var delay = await clock.Delays.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(TimeSpan.FromMilliseconds(100), delay);
                Assert.IsFalse(run.IsCompleted, "A new run must not inherit the previous run's shutdown signal.");
            }
            finally
            {
                cancellation.Cancel();
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        Assert.AreEqual(2, builds);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_ContinuousInvalidation_ServicesTimerAndShutdown(bool cancel)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? workload = null;
        var frames = 0;
        var timerFrame = 0;
        var stopFrame = 0;
        var observer = new TestWidget().OnRender(args =>
        {
            frames = args.RenderCount;
            app!.Invalidate();
            workload!.SendKey(Hex1bKey.W);
            if (timerFrame > 0 && frames >= timerFrame + 3 && stopFrame == 0)
            {
                stopFrame = frames;
                if (cancel)
                    cancellation.Cancel();
                else
                    app.RequestStop();
            }
        });
        var timer = new TimerProbeWidget(() => timerFrame = frames);
        var builds = 0;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options =>
                {
                    workload = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter);
                    options.EnableInputCoalescing = false;
                    options.EnableRescue = false;
                },
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([
                        new TextBlockWidget($"Frame {++builds}"), timer, observer,
                    ]);
                })
            .WithHeadless()
            .WithDimensions(24, 6)
            .Build();

        var runTask = Task.Run(() => terminal.RunAsync(cancellation.Token), cancellation.Token);
        try
        {
            await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.IsGreaterThan(0, timerFrame, "A timer must fire while the producer is still active.");
            Assert.IsGreaterThan(0, stopFrame, "The app must progress beyond the timer before stopping.");
            Assert.AreEqual(stopFrame, frames, "Queued invalidations must not prevent shutdown.");
        }
        finally
        {
            cancellation.Cancel();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    private sealed class FrameTimeProvider : FakeTimeProvider
    {
        private readonly long _origin;

        public FrameTimeProvider() => _origin = base.GetTimestamp();

        public override long GetTimestamp() => base.GetTimestamp() - _origin;

        public Channel<TimeSpan> Delays { get; } = Channel.CreateUnbounded<TimeSpan>();
        public Channel<TimeSpan> Changes { get; } = Channel.CreateUnbounded<TimeSpan>();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (dueTime != Timeout.InfiniteTimeSpan)
                Delays.Writer.TryWrite(dueTime);
            return new ObservedTimer(timer, Changes);
        }
    }

    private sealed class ObservedTimer(ITimer timer, Channel<TimeSpan> changes) : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            var changed = timer.Change(dueTime, period);
            if (dueTime != Timeout.InfiniteTimeSpan)
                changes.Writer.TryWrite(dueTime);
            return changed;
        }

        public void Dispose() => timer.Dispose();
        public ValueTask DisposeAsync() => timer.DisposeAsync();
    }

    private sealed record AnimationProbeWidget(Action<ReconcileContext> Observe) : Hex1bWidget
    {
        internal override Task<Hex1bNode> ReconcileAsync(
            Hex1bNode? existingNode, ReconcileContext context)
        {
            Observe(context);
            return Task.FromResult(existingNode ?? new TestWidgetNode());
        }

        internal override Type GetExpectedNodeType() => typeof(TestWidgetNode);
    }

    private sealed record TimerProbeWidget(Action Fired) : Hex1bWidget
    {
        internal override Task<Hex1bNode> ReconcileAsync(Hex1bNode? existingNode, ReconcileContext context)
        {
            if (existingNode is null)
            {
                Assert.IsNotNull(context.ScheduleTimerCallback);
                context.ScheduleTimerCallback(TimeSpan.FromMilliseconds(16), Fired);
            }
            return Task.FromResult<Hex1bNode>(existingNode ?? new TestWidgetNode());
        }

        internal override Type GetExpectedNodeType() => typeof(TestWidgetNode);
    }
}
