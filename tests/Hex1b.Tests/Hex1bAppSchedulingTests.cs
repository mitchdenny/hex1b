using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Widgets;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class Hex1bAppSchedulingTests
{
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
    [DataRow(16)]
    [DataRow(100)]
    public async Task RunAsync_ContinuousInvalidation_RespectsFrameRateLimit(int frameRateLimitMs)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var clock = new FrameTimeProvider();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        var interval = TimeSpan.FromMilliseconds(frameRateLimitMs);
        Hex1bApp? app = null;
        var timestamps = new List<long>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observer = new TestWidget().OnRender(args =>
        {
            timestamps.Add(clock.GetTimestamp());
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
            // The first two frames are unpaced; each subsequent frame needs a full budget.
            for (var i = 2; i < 6; i++)
            {
                var delay = await clock.Delays.Reader.ReadAsync(TestContext.Current.CancellationToken)
                    .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.AreEqual(interval, delay, $"Frame {i + 1} must wait for the configured budget.");
                clock.Advance(interval);
            }
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        finally
        {
            cancellation.Cancel();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        for (var i = 2; i < 6; i++)
        {
            Assert.AreEqual(interval, clock.GetElapsedTime(timestamps[i - 1], timestamps[i]),
                $"Frames {i} and {i + 1} must be one budget apart.");
        }
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
        public Channel<TimeSpan> Delays { get; } = Channel.CreateUnbounded<TimeSpan>();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            Delays.Writer.TryWrite(dueTime);
            return timer;
        }
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
