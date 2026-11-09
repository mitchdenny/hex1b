using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class TerminalProgramStatusReporterTests
{
    [TestMethod]
    public async Task ReportAsync_AwaitingCallback_EmitsTransitionsWithoutRedrawing()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var steps = Channel.CreateUnbounded<bool>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Hex1bApp? app = null;
        RootContext? root = null;
        var builds = 0;
        var returned = false;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(options => options.EnableRescue = false, instance =>
            {
                app = instance;
                return context =>
                {
                    root = context;
                    Interlocked.Increment(ref builds);
                    return context.Button(returned ? "Finished" : "Run").OnClick(async e =>
                    {
                        Assert.AreSame(instance.ProgramStatus, e.Context.ProgramStatus);
                        entered.TrySetResult();
                        await e.Context.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Working)
                        {
                            App = "test", Progress = 10, Title = "Fetching", Message = "Please wait"
                        }, e.Context.CancellationToken);
                        await steps.Reader.ReadAsync(e.Context.CancellationToken);
                        await e.Context.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Blocked)
                        {
                            Kind = TerminalProgramStatusKind.Permission
                        }, e.Context.CancellationToken);
                        await steps.Reader.ReadAsync(e.Context.CancellationToken);
                        await e.Context.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Working), e.Context.CancellationToken);
                        await steps.Reader.ReadAsync(e.Context.CancellationToken);
                        await e.Context.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Done), e.Context.CancellationToken);
                        await steps.Reader.ReadAsync(e.Context.CancellationToken);
                        await e.Context.ProgramStatus.ClearAsync(cancellationToken: e.Context.CancellationToken);
                        returned = true;
                        finished.TrySetResult();
                    });
                };
            })
            .WithHeadless().WithDimensions(30, 5).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Run"), TimeSpan.FromSeconds(5), "button ready")
                .Key(Hex1bKey.Enter).Build().ApplyAsync(terminal, cancellation.Token);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            var initialBuilds = Volatile.Read(ref builds);
            Assert.AreSame(app!.ProgramStatus, root!.ProgramStatus);
            foreach (var state in new[] { TerminalProgramStatusState.Working, TerminalProgramStatusState.Blocked,
                         TerminalProgramStatusState.Working, TerminalProgramStatusState.Done })
            {
                using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
                    .WaitUntil(s => s.ProgramStatus.Find("")?.State == state, TimeSpan.FromSeconds(5), $"{state} emitted")
                    .Build().ApplyAsync(terminal, cancellation.Token);
                var record = snapshot.ProgramStatus.Find("")!;
                if (state == TerminalProgramStatusState.Blocked)
                {
                    Assert.AreEqual(TerminalProgramStatusKind.Permission, record.Kind);
                    Assert.IsNull(record.App);
                    Assert.IsNull(record.Title);
                    Assert.IsNull(record.Message);
                    Assert.IsNull(record.Progress);
                }
                Assert.AreEqual(initialBuilds, Volatile.Read(ref builds), "Reporting must not require a redraw.");
                steps.Writer.TryWrite(true);
            }
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ProgramStatus.Records.Count == 0 && s.ContainsText("Finished"),
                    TimeSpan.FromSeconds(5), "root remains cleared after callback returns")
                .Build().ApplyAsync(terminal, cancellation.Token);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [TestMethod]
    public async Task Report_RawSerialization_RoundTripsOptionalFieldsAndClear()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var app = new Hex1bApp(context => context.Text("Unused"), new() { WorkloadAdapter = workload });
        var report = new TerminalProgramStatusReport(TerminalProgramStatusState.Blocked)
        {
            Id = "build/test", App = "worker", Kind = TerminalProgramStatusKind.Question,
            Progress = 42, Title = "Ready?", Message = "héllo 😀"
        };
        app.ProgramStatus.Report(report);
        var bytes = await workload.ReadOutputAsync(TestContext.Current.CancellationToken);
        var expected = "\x1b]7501;state=blocked:id=build/test:kind=question:progress=42:app=worker:title=UmVhZHk/:msg=" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(report.Message)) + "\x1b\\";
        Assert.AreEqual(expected, Encoding.UTF8.GetString(bytes.Span));
        Assert.IsTrue(TerminalProgramStatusCodec.TryParse(expected[7..^2], bytes.Length, out var parsed, out var clear));
        Assert.AreEqual(new TerminalProgramStatusRecord("build/test", report.State, report.Kind, report.Progress,
            report.App, report.Title, report.Message), parsed);
        Assert.IsNull(clear);
        app.ProgramStatus.Clear("build/test");
        Assert.AreEqual("\x1b]7501;state=clear:id=build/test\x1b\\",
            Encoding.UTF8.GetString((await workload.ReadOutputAsync(TestContext.Current.CancellationToken)).Span));
        await app.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Idle));
        Assert.AreEqual("\x1b]7501;state=idle\x1b\\",
            Encoding.UTF8.GetString((await workload.ReadOutputAsync(TestContext.Current.CancellationToken)).Span));
        await app.ProgramStatus.ClearAsync();
        Assert.AreEqual("\x1b]7501;state=clear\x1b\\",
            Encoding.UTF8.GetString((await workload.ReadOutputAsync(TestContext.Current.CancellationToken)).Span));
    }

    [TestMethod]
    public void Report_InvalidInput_ThrowsWithoutEmittingAnyOutput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var app = new Hex1bApp(context => context.Text("Unused"), new() { WorkloadAdapter = workload });
        var valid = new TerminalProgramStatusReport(TerminalProgramStatusState.Working);
        var invalid = new[]
        {
            valid with { State = (TerminalProgramStatusState)99 },
            valid with { Kind = (TerminalProgramStatusKind)99 },
            valid with { Kind = TerminalProgramStatusKind.Auth },
            valid with { Progress = -1 }, valid with { Progress = 101 },
            valid with { State = TerminalProgramStatusState.Done, Progress = 50 },
            valid with { Id = "/child" }, valid with { Id = "a//b" },
            valid with { Id = new string('a', 33) }, valid with { Id = "a/b/c/d/e/f/g/h/i" },
            valid with { Id = "a:b" }, valid with { App = "a b" }, valid with { App = new string('a', 33) },
            valid with { Title = "\x1b]7501;state=done" }, valid with { Message = "\u0085" },
            valid with { Title = new string('x', 193) }, valid with { Message = new string('x', 2049) },
            valid with { Title = new string('é', 97) }, valid with { Message = "\ud800" }
        };
        foreach (var report in invalid)
        {
            Assert.Throws<ArgumentException>(() => app.ProgramStatus.Report(report));
            Assert.Throws<ArgumentException>(() => app.ProgramStatus.ReportAsync(report));
        }
        Assert.ThrowsExactly<ArgumentNullException>(() => app.ProgramStatus.Report(null!));
        Assert.Throws<ArgumentException>(() => app.ProgramStatus.Clear("bad/id/"));
        Assert.Throws<ArgumentException>(() => app.ProgramStatus.ClearAsync("bad:id"));
        Assert.AreEqual(0, workload.OutputQueueDepth, "Invalid reports must be atomic.");
    }

    [TestMethod]
    public async Task ReportAsync_BoundedOutput_CancellationAndDisposalUnblockPendingWrites()
    {
        using var workload = new Hex1bAppWorkloadAdapter(maxQueuedOutputItems: 1);
        await using var app = new Hex1bApp(context => context.Text("Unused"), new() { WorkloadAdapter = workload });
        app.ProgramStatus.Report(new(TerminalProgramStatusState.Working));
        using var cancellation = new CancellationTokenSource();
        var canceled = app.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Blocked), cancellation.Token);
        Assert.IsFalse(canceled.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => canceled);
        Assert.AreEqual(1, workload.OutputQueueDepth);
        var pending = app.ProgramStatus.ClearAsync();
        Assert.IsFalse(pending.IsCompleted);
        await app.DisposeAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        Assert.ThrowsExactly<ObjectDisposedException>(() => app.ProgramStatus.Report(new(TerminalProgramStatusState.Done)));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => app.ProgramStatus.ClearAsync());
    }

    [TestMethod]
    public void ProgramStatus_StandaloneContexts_ThrowExplicitly()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new RootContext().ProgramStatus);
        Assert.ThrowsExactly<InvalidOperationException>(() => new InputBindingActionContext(new FocusRing()).ProgramStatus);
    }

    [TestMethod]
    public async Task ReportAsync_CustomAdapter_DoesNotInterleaveWithRendering()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var adapter = new RecordingAdapter();
        await using var app = new Hex1bApp(context => context.Text("Hello"),
            new() { WorkloadAdapter = adapter, EnableRescue = false });
        var run = Task.Run(() => app.RunAsync(cancellation.Token), cancellation.Token);
        try
        {
            await adapter.FrameStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            var report = app.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Working));
            Assert.IsFalse(report.IsCompleted, "A report must wait for the entire render output transaction.");
            adapter.ReleaseFrame.Set();
            await report.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            var writes = adapter.GetWrites();
            var end = Array.IndexOf(writes, "\x1b[?2026l");
            var status = Array.IndexOf(writes, "\x1b]7501;state=working\x1b\\");
            Assert.IsTrue(end >= 0 && status > end);
            Assert.AreEqual(1, adapter.FlushCount, "The fallback adapter must be flushed without waiting for a redraw.");
        }
        finally
        {
            adapter.ReleaseFrame.Set();
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ProgramStatus_MouseAndPasteCallbacks_ReceiveAppService(bool paste)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Hex1bAppWorkloadAdapter? workload = null;
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(options =>
            {
                workload = (Hex1bAppWorkloadAdapter)options.WorkloadAdapter!;
                options.EnableRescue = false;
            }, app => context => paste
                ? context.TextBox("Ready").OnTextChanged(e =>
                {
                    Assert.AreSame(app.ProgramStatus, e.Context.ProgramStatus);
                    e.Context.ProgramStatus.Report(new(TerminalProgramStatusState.Done));
                    invoked.TrySetResult();
                })
                : context.Button("Ready").OnClick(e =>
                {
                    Assert.AreSame(app.ProgramStatus, e.Context.ProgramStatus);
                    e.Context.ProgramStatus.Report(new(TerminalProgramStatusState.Done));
                    invoked.TrySetResult();
                }))
            .WithHeadless().WithDimensions(30, 5).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Ready"), TimeSpan.FromSeconds(5), "input target ready")
                .Build().ApplyAsync(terminal, cancellation.Token);
            if (paste)
            {
                await using var content = new PasteContext();
                content.TryWrite("pasted");
                content.Complete();
                await workload!.WriteInputEventAsync(new Hex1bPasteEvent(content), cancellation.Token);
                await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            }
            else
            {
                workload!.SendMouse(MouseButton.Left, MouseAction.Down, 1, 0);
                await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            }
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ProgramStatus.Find("")?.State == TerminalProgramStatusState.Done,
                    TimeSpan.FromSeconds(5), "callback report parsed")
                .Build().ApplyAsync(terminal, cancellation.Token);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [TestMethod]
    [DataRow("Report")]
    [DataRow("ReportAsync")]
    [DataRow("Clear")]
    [DataRow("ClearAsync")]
    [DataRow("Clipboard")]
    public async Task RenderCallback_ReentrantEmission_ThrowsWithoutDeadlocking(string operation)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var rejected = new TaskCompletionSource<InvalidOperationException>(TaskCreationOptions.RunContinuationsAsynchronously);
        Hex1bApp? app = null;
        var observer = new TestWidget().OnRender(_ =>
        {
            var exception = Assert.ThrowsExactly<InvalidOperationException>(() =>
            {
                switch (operation)
                {
                    case "Report":
                        app!.ProgramStatus.Report(new(TerminalProgramStatusState.Working));
                        break;
                    case "ReportAsync":
                        app!.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Working)).GetAwaiter().GetResult();
                        break;
                    case "Clear":
                        app!.ProgramStatus.Clear();
                        break;
                    case "ClearAsync":
                        app!.ProgramStatus.ClearAsync().GetAwaiter().GetResult();
                        break;
                    case "Clipboard":
                        app!.CopyToClipboard("text");
                        break;
                }
            });
            rejected.TrySetResult(exception);
        });
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(options => options.EnableRescue = false, instance =>
            {
                app = instance;
                return context => context.VStack(children => [children.Text("Rendered"), observer]);
            })
            .WithHeadless().WithDimensions(30, 5).Build();
        var run = Task.Run(() => terminal.RunAsync(cancellation.Token), cancellation.Token);
        try
        {
            var exception = await rejected.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            StringAssert.Contains(exception.Message, "render callback");
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Rendered") && s.ProgramStatus.Records.Count == 0,
                    TimeSpan.FromSeconds(5), "frame completed without reentrant output")
                .Build().ApplyAsync(terminal, cancellation.Token);
            await app!.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Done));
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ProgramStatus.Find("")?.State == TerminalProgramStatusState.Done,
                    TimeSpan.FromSeconds(5), "reporting still works after the rejected callback")
                .Build().ApplyAsync(terminal, cancellation.Token);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [TestMethod]
    public async Task RenderCallback_BackgroundEmissionAfterFrame_AllowsInheritedContext()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Hex1bApp? app = null;
        var observer = new TestWidget().OnRender(args =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await resume.Task.WaitAsync(cancellation.Token);
                    await app!.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Working), cancellation.Token);
                    finished.TrySetResult();
                }
                catch (Exception exception)
                {
                    finished.TrySetException(exception);
                }
            }, cancellation.Token);
        });
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(options => options.EnableRescue = false, instance =>
            {
                app = instance;
                return context => context.VStack(children => [children.Text("Rendered"), observer]);
            })
            .WithHeadless().WithDimensions(30, 5).Build();
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Rendered"), TimeSpan.FromSeconds(5), "initial frame completed")
                .Build().ApplyAsync(terminal, cancellation.Token);
            await app!.ProgramStatus.ReportAsync(new(TerminalProgramStatusState.Idle), cancellation.Token);
            resume.TrySetResult();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellation.Token);
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ProgramStatus.Find("")?.State == TerminalProgramStatusState.Working,
                    TimeSpan.FromSeconds(5), "inherited background context can report after frame completion")
                .Build().ApplyAsync(terminal, cancellation.Token);
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    private sealed class RecordingAdapter : IHex1bAppTerminalWorkloadAdapter
    {
        private readonly Channel<Hex1bEvent> _input = Channel.CreateUnbounded<Hex1bEvent>();
        private readonly List<string> _writes = [];
        public TaskCompletionSource FrameStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ReleaseFrame { get; } = new();
        public int FlushCount { get; private set; }
        public ChannelReader<Hex1bEvent> InputEvents => _input.Reader;
        public int Width => 30;
        public int Height => 5;
        public TerminalCapabilities Capabilities { get; } = new();
        public int OutputQueueDepth => 0;
        public event Action? Disconnected { add { } remove { } }

        public void Write(string text)
        {
            lock (_writes) _writes.Add(text);
            if (text == "\x1b[?2026h")
            {
                FrameStarted.TrySetResult();
                Assert.IsTrue(ReleaseFrame.Wait(TimeSpan.FromSeconds(5)), "The controlled frame must be released.");
            }
        }

        public string[] GetWrites() { lock (_writes) return _writes.ToArray(); }
        public void Write(ReadOnlySpan<byte> data) => Write(Encoding.UTF8.GetString(data));
        public void Flush() => FlushCount++;
        public void EnterTuiMode() { }
        public void ExitTuiMode() { }
        public void Clear() => Write("\x1b[2J");
        public void SetCursorPosition(int left, int top) => Write($"\x1b[{top + 1};{left + 1}H");
        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => ValueTask.FromResult(ReadOnlyMemory<byte>.Empty);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            ReleaseFrame.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
