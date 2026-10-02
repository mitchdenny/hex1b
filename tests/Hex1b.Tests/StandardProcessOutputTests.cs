using System.Diagnostics;
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class StandardProcessOutputTests
{
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(7, false)]
    [DataRow(0, true)]
    [DataRow(7, true)]
    public async Task DrainAsync_ProcessAlreadyExited_TransmitsEveryOutputCellAndByteBeforeFinalAck(int exitCode, bool filtered)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        await using var workload = new StandardProcessWorkloadAdapter(CreateStartInfo(exitCode));
        await workload.StartAsync(ct);
        Assert.AreEqual(exitCode, await workload.WaitForExitAsync(ct));
        await using var presentation = new Hwt1PresentationAdapter(80, 10);
        var options = new Hex1bTerminalOptions
        {
            Width = 80, Height = 10, WorkloadAdapter = workload, PresentationAdapter = presentation,
            RunCallback = _ => Task.FromResult(workload.ExitCode)
        };
        if (filtered)
            options.PresentationFilters.Add(new PassthroughPresentationFilter());
        await using var terminal = new Hex1bTerminal(options);

        // Hold an empty initial frame while the already-exited child is drained.
        var initial = await presentation.ReadFrameAsync(ct);
        Assert.AreEqual(exitCode, await terminal.RunAsync(ct));
        var drain = presentation.DrainAsync(ct);
        var finalRead = presentation.ReadFrameAsync(ct).AsTask();
        Assert.IsFalse(finalRead.IsCompleted);
        Assert.IsFalse(drain.IsCompleted);
        await presentation.HandleMessageAsync("""{"type":"ack","revision":1}"""u8.ToArray(), ct);
        var final = await finalRead.WaitAsync(ct);
        Assert.IsFalse(drain.IsCompleted, "Sending bytes is not proof that the final view was presented.");

        var received = new string[800];
        ApplyFrame(initial, received);
        ApplyFrame(final, received);
        using var snapshot = terminal.CreateSnapshot();
        for (var row = 0; row < 10; row++)
        {
            for (var column = 0; column < 80; column++)
                Assert.AreEqual(snapshot.GetCell(column, row).Character, received[row * 80 + column],
                    $"Transmitted cell {column},{row}");
        }
        var text = string.Concat(received);
        Assert.Contains("FirstOutput", text);
        Assert.Contains("FinalOutput", text);
        Assert.Contains("ErrorOutput", text);
        var length = BinaryPrimitives.ReadInt32LittleEndian(final.Span[4..]);
        using var metadata = JsonDocument.Parse(final.Slice(8, length));
        var expectedBytes = Encoding.UTF8.GetByteCount("FirstOutput\nFinalOutput\nErrorOutput\n");
        Assert.AreEqual((long)expectedBytes,
            metadata.RootElement.GetProperty("stats").GetProperty("workloadBytes").GetInt64());
        await presentation.HandleMessageAsync(JsonSerializer.SerializeToUtf8Bytes(new
        {
            type = "ack", revision = metadata.RootElement.GetProperty("revision").GetUInt32()
        }), ct);
        await drain.WaitAsync(ct);
    }

    private static void ApplyFrame(ReadOnlyMemory<byte> frame, string[] cells)
    {
        var span = frame.Span;
        var offset = 8 + BinaryPrimitives.ReadInt32LittleEndian(span[4..]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
        offset += 4;
        for (var index = 0; index < count; index++)
        {
            var cellIndex = BinaryPrimitives.ReadInt32LittleEndian(span[offset..]);
            var length = BinaryPrimitives.ReadUInt16LittleEndian(span[(offset + 20)..]);
            cells[cellIndex] = Encoding.UTF8.GetString(span.Slice(offset + 22, length));
            offset += 22 + length;
        }
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(7, false)]
    [DataRow(0, true)]
    [DataRow(7, true)]
    public async Task RunAsync_ProcessAlreadyExited_DrainsOutputBeforeCompleting(int exitCode, bool filtered)
    {
        await using var adapter = new StandardProcessWorkloadAdapter(CreateStartInfo(exitCode));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await adapter.StartAsync(cts.Token);
        Assert.AreEqual(exitCode, await adapter.WaitForExitAsync(cts.Token));

        var presentation = new GatedPresentationAdapter();
        var options = new Hex1bTerminalOptions
        {
            WorkloadAdapter = adapter,
            PresentationAdapter = presentation,
            RunCallback = _ => Task.FromResult(adapter.ExitCode)
        };
        if (filtered)
            options.PresentationFilters.Add(new PassthroughPresentationFilter());
        await using var terminal = new Hex1bTerminal(options);

        var runTask = terminal.RunAsync(cts.Token);
        try
        {
            // The child has exited, but the first write and all subsequent reads
            // are deliberately blocked until the test releases the presentation.
            Assert.IsFalse(runTask.IsCompleted, "Process exit must not complete the terminal before output is drained.");
            await presentation.WriteStarted.Task.WaitAsync(cts.Token);
            Assert.IsNull(presentation.CompletedExitCode);

            presentation.ReleaseWrite.TrySetResult();
            Assert.AreEqual(exitCode, await runTask.WaitAsync(cts.Token));
            Assert.AreEqual(exitCode, presentation.CompletedExitCode);
            Assert.IsTrue(presentation.CompletedAfterOutput);
            Assert.Contains("FirstOutput", presentation.Output.ToString());
            Assert.Contains("FinalOutput", presentation.Output.ToString());
            Assert.Contains("ErrorOutput", presentation.Output.ToString());
            var snapshot = terminal.CreateSnapshot();
            Assert.IsTrue(snapshot.ContainsText("FirstOutput"));
            Assert.IsTrue(snapshot.ContainsText("FinalOutput"));
            Assert.IsTrue(snapshot.ContainsText("ErrorOutput"));
        }
        finally
        {
            presentation.ReleaseWrite.TrySetResult();
            await cts.CancelAsync();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_WhileDrainingOutput_PropagatesCancellationOrPumpFailure(bool failOutput)
    {
        await using var adapter = new StandardProcessWorkloadAdapter(CreateStartInfo(0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        await adapter.StartAsync(timeout.Token);
        await adapter.WaitForExitAsync(timeout.Token);

        var presentation = new GatedPresentationAdapter();
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            WorkloadAdapter = adapter,
            PresentationAdapter = presentation,
            RunCallback = _ => Task.FromResult(adapter.ExitCode)
        });

        var runTask = terminal.RunAsync(runCts.Token);
        try
        {
            await presentation.WriteStarted.Task.WaitAsync(timeout.Token);
            if (failOutput)
            {
                var error = new IOException("Synthetic output failure after process exit.");
                presentation.WriteError = error;
                presentation.ReleaseWrite.TrySetResult();
                var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
                    () => runTask.WaitAsync(timeout.Token));
                Assert.AreSame(error, exception.InnerException);
                Assert.Contains("workload output pump", exception.Message);
            }
            else
            {
                await runCts.CancelAsync();
                await Assert.ThrowsAsync<OperationCanceledException>(() => runTask.WaitAsync(timeout.Token));
            }
            Assert.IsNull(presentation.CompletedExitCode);
        }
        finally
        {
            presentation.WriteError = null;
            presentation.ReleaseWrite.TrySetResult();
            await runCts.CancelAsync();
        }
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(7, false)]
    [DataRow(0, true)]
    [DataRow(7, true)]
    public async Task WithProcess_QuickExit_PreservesOutputAndExitCode(int exitCode, bool silent)
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithProcess(CreateStartInfo(exitCode, silent))
            .WithHeadless()
            .WithDimensions(80, 10)
            .Build();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        Assert.AreEqual(exitCode, await terminal.RunAsync(cts.Token));
        if (!silent)
        {
            var snapshot = terminal.CreateSnapshot();
            Assert.IsTrue(snapshot.ContainsText("FirstOutput"));
            Assert.IsTrue(snapshot.ContainsText("FinalOutput"));
            Assert.IsTrue(snapshot.ContainsText("ErrorOutput"));
        }
    }

    [TestMethod]
    public async Task WaitForExitAsync_BeforeReadingOutput_PreservesBothStreams()
    {
        await using var adapter = new StandardProcessWorkloadAdapter(CreateStartInfo(7));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnectCount = 0;
        adapter.Disconnected += () =>
        {
            Interlocked.Increment(ref disconnectCount);
            disconnected.TrySetResult();
        };

        await adapter.StartAsync(cts.Token);
        Assert.AreEqual(7, await adapter.WaitForExitAsync(cts.Token));
        await disconnected.Task.WaitAsync(cts.Token);

        var output = new StringBuilder();
        while (true)
        {
            var data = await adapter.ReadOutputAsync(cts.Token);
            if (data.IsEmpty)
                break;
            output.Append(Encoding.UTF8.GetString(data.Span));
        }

        Assert.AreEqual(1, disconnectCount);
        Assert.IsTrue(adapter.HasExited);
        Assert.Contains("FirstOutput\n", output.ToString());
        Assert.Contains("FinalOutput\n", output.ToString());
        Assert.Contains("ErrorOutput\n", output.ToString());
    }

    private static ProcessStartInfo CreateStartInfo(int exitCode, bool silent = false)
    {
        var startInfo = new ProcessStartInfo(OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh");
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(silent
                ? $"exit /b {exitCode}"
                : $"echo FirstOutput&echo FinalOutput&echo ErrorOutput>&2&exit /b {exitCode}");
        }
        else
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(silent
                ? $"exit {exitCode}"
                : $"printf 'FirstOutput\\nFinalOutput\\n'; printf 'ErrorOutput' >&2; exit {exitCode}");
        }
        return startInfo;
    }

    private sealed class GatedPresentationAdapter : IHex1bTerminalPresentationAdapter, ITerminalLifecycleAwarePresentationAdapter
    {
        private readonly HeadlessPresentationAdapter _headless = new(80, 10);
        private Hex1bTerminal? _terminal;

        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public StringBuilder Output { get; } = new();
        public int? CompletedExitCode { get; private set; }
        public bool CompletedAfterOutput { get; private set; }
        public Exception? WriteError { get; set; }
        public int Width => _headless.Width;
        public int Height => _headless.Height;
        public TerminalCapabilities Capabilities => _headless.Capabilities;
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }

        public async ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            WriteStarted.TrySetResult();
            await ReleaseWrite.Task.WaitAsync(ct);
            if (WriteError is { } error)
                throw error;
            Output.Append(Encoding.UTF8.GetString(data.Span));
        }

        public void TerminalCreated(Hex1bTerminal terminal) => _terminal = terminal;
        public void TerminalStarted() { }
        public void TerminalCompleted(int exitCode)
        {
            CompletedExitCode = exitCode;
            var snapshot = _terminal!.CreateSnapshot();
            CompletedAfterOutput = snapshot.ContainsText("FirstOutput")
                && snapshot.ContainsText("FinalOutput")
                && snapshot.ContainsText("ErrorOutput");
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default) => _headless.ReadInputAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => _headless.FlushAsync(ct);
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => _headless.EnterRawModeAsync(ct);
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => _headless.ExitRawModeAsync(ct);
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => _headless.DisposeAsync();
    }

    private sealed class PassthroughPresentationFilter : IHex1bTerminalPresentationFilter
    {
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<AnsiToken>>(appliedTokens.Select(t => t.Token).ToArray());

        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }
}
