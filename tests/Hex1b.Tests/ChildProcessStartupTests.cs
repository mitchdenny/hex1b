using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Hex1b.Tests;

[TestClass]
public class ChildProcessStartupTests
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(5);

    [TestMethod]
    public async Task StartAsync_FactoryFailure_FaultsBothStartupWaitersAndRemainsSingleUse()
    {
        var failure = new InvalidOperationException("factory failed");
        await using var process = Create(_ => throw failure);
        var read = process.ReadOutputAsync().AsTask();
        var write = process.WriteInputAsync(new byte[] { 1 }).AsTask();

        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => process.StartAsync().WaitAsync(Watchdog)));
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => read.WaitAsync(Watchdog)));
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => write.WaitAsync(Watchdog)));
        await AssertFailedStateAsync(process);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.StartAsync());
    }

    [TestMethod]
    public async Task StartAsync_Timeout_CleansPartialHandleBeforeFaultingWaiters()
    {
        var failure = new TimeoutException("startup checkpoint timeout");
        var handle = new ControlledPtyHandle { StartupError = failure, HoldDisposal = true };
        await using var process = Create(_ => handle);
        var read = process.ReadOutputAsync().AsTask();
        var write = process.WriteInputAsync(new byte[] { 1 }).AsTask();
        var start = process.StartAsync();
        handle.ReleaseStart.TrySetResult();
        await handle.DisposalEntered.Task.WaitAsync(Watchdog);
        try
        {
            Assert.IsFalse(start.IsCompleted);
            Assert.IsFalse(read.IsCompleted);
            Assert.IsFalse(write.IsCompleted);
            Assert.AreEqual(9, handle.KillSignal);
            Assert.AreEqual(1, handle.WaitCount);
            Assert.AreEqual(-1, process.ProcessId);
        }
        finally
        {
            handle.ReleaseDisposal.TrySetResult();
        }

        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<TimeoutException>(() => start.WaitAsync(Watchdog)));
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<TimeoutException>(() => read.WaitAsync(Watchdog)));
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<TimeoutException>(() => write.WaitAsync(Watchdog)));
        await AssertFailedStateAsync(process);
        await process.DisposeAsync();
        Assert.AreEqual(1, handle.DisposeCount);
    }

    [TestMethod]
    public async Task StartAsync_CleanupFailure_PreservesPrimaryErrorAndSettlesGate()
    {
        var failure = new InvalidOperationException("native launch failed");
        var handle = new ControlledPtyHandle
        {
            StartupError = failure,
            KillError = new IOException("kill failed"),
            DisposalError = new IOException("dispose failed")
        };
        handle.ReleaseStart.TrySetResult();
        await using var process = Create(_ => handle);

        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.StartAsync()));
        Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => process.WriteInputAsync(new byte[] { 1 }).AsTask().WaitAsync(Watchdog)));
        var cleanup = TestSeq.IsType<AggregateException>(failure.Data["Hex1b.StartupCleanupErrors"]);
        Assert.AreEqual(2, cleanup.InnerExceptions.Count);
        Assert.AreEqual(1, handle.DisposeCount);
        await AssertFailedStateAsync(process);
    }

    [TestMethod]
    public async Task StartAsync_CallerCancellation_CancelsBothGatesAfterCleanup()
    {
        var handle = new ControlledPtyHandle();
        await using var process = Create(_ => handle);
        using var cancellation = new CancellationTokenSource();
        var read = process.ReadOutputAsync().AsTask();
        var write = process.WriteInputAsync(new byte[] { 1 }).AsTask();
        var start = process.StartAsync(cancellation.Token);
        await handle.StartEntered.Task.WaitAsync(Watchdog);
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => start.WaitAsync(Watchdog));
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        await Assert.ThrowsAsync<OperationCanceledException>(() => read.WaitAsync(Watchdog));
        await Assert.ThrowsAsync<OperationCanceledException>(() => write.WaitAsync(Watchdog));
        Assert.IsTrue(read.IsCanceled);
        Assert.IsTrue(write.IsCanceled);
        Assert.AreEqual(1, handle.DisposeCount);
        await AssertFailedStateAsync(process);
    }

    [TestMethod]
    public async Task StartAsync_AlreadyCanceled_DoesNotCreateHandleAndSettlesGate()
    {
        var created = false;
        await using var process = Create(_ =>
        {
            created = true;
            return new ControlledPtyHandle();
        });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => process.StartAsync(cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(() => process.ReadOutputAsync().AsTask().WaitAsync(Watchdog));
        Assert.IsFalse(created);
        await AssertFailedStateAsync(process);
    }

    [TestMethod]
    public async Task WriteInputAsync_CallerCancelsStartupWait_DoesNotCancelStartup()
    {
        var handle = new ControlledPtyHandle();
        await using var process = Create(_ => handle);
        var start = process.StartAsync();
        using var cancellation = new CancellationTokenSource();
        var write = process.WriteInputAsync(new byte[] { 1 }, cancellation.Token).AsTask();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => write.WaitAsync(Watchdog));
        Assert.IsFalse(start.IsCompleted);
        handle.ReleaseStart.TrySetResult();
        await start.WaitAsync(Watchdog);
        Assert.IsTrue(process.HasStarted);
        Assert.AreEqual(0, handle.WriteCount);
    }

    [TestMethod]
    public async Task ReadAndWrite_AfterStartup_PreserveBestEffortCancellation()
    {
        var handle = new ControlledPtyHandle { CancelIo = true };
        handle.ReleaseStart.TrySetResult();
        await using var process = Create(_ => handle);
        await process.StartAsync();

        Assert.IsTrue((await process.ReadOutputAsync()).IsEmpty);
        await process.WriteInputAsync(new byte[] { 1 });
        Assert.AreEqual(1, handle.WriteCount);
    }

    [TestMethod]
    public async Task DisposeAsync_BeforeStartup_FaultsExistingAndFutureWaiters()
    {
        var created = false;
        var process = Create(_ =>
        {
            created = true;
            return new ControlledPtyHandle();
        });
        var read = process.ReadOutputAsync().AsTask();
        var write = process.WriteInputAsync(new byte[] { 1 }).AsTask();

        await process.DisposeAsync().AsTask().WaitAsync(Watchdog);
        await process.DisposeAsync();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => read.WaitAsync(Watchdog));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => write.WaitAsync(Watchdog));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => process.ReadOutputAsync().AsTask());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => process.WriteInputAsync(new byte[] { 1 }).AsTask());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => process.StartAsync());
        Assert.IsFalse(created);
        await AssertFailedStateAsync(process);
    }

    [TestMethod]
    public async Task DisposeAsync_DuringUncancellableStartup_PreventsLatePublicationAndWaitsForCleanup()
    {
        var handle = new ControlledPtyHandle { IgnoreCancellation = true, HoldDisposal = true };
        var process = Create(_ => handle);
        var start = process.StartAsync();
        await handle.StartEntered.Task.WaitAsync(Watchdog);
        var dispose = process.DisposeAsync().AsTask();
        var secondDispose = process.DisposeAsync().AsTask();
        Assert.AreSame(dispose, secondDispose);
        Assert.IsFalse(dispose.IsCompleted);
        handle.ReleaseStart.TrySetResult();
        await handle.DisposalEntered.Task.WaitAsync(Watchdog);
        try
        {
            Assert.IsFalse(dispose.IsCompleted);
            Assert.IsFalse(start.IsCompleted);
            Assert.AreEqual(9, handle.KillSignal);
        }
        finally
        {
            handle.ReleaseDisposal.TrySetResult();
        }

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => start.WaitAsync(Watchdog));
        await dispose.WaitAsync(Watchdog);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => process.ReadOutputAsync().AsTask());
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => process.WriteInputAsync(new byte[] { 1 }).AsTask());
        Assert.AreEqual(1, handle.DisposeCount);
        await AssertFailedStateAsync(process);
    }

    [TestMethod]
    public async Task DisposeAsync_DuringCancellableStartup_CancelsAndCleansOnce()
    {
        var handle = new ControlledPtyHandle();
        var process = Create(_ => handle);
        var start = process.StartAsync();
        await handle.StartEntered.Task.WaitAsync(Watchdog);

        await process.DisposeAsync().AsTask().WaitAsync(Watchdog);
        await Assert.ThrowsAsync<OperationCanceledException>(() => start.WaitAsync(Watchdog));
        await Assert.ThrowsAsync<OperationCanceledException>(() => process.WriteInputAsync(new byte[] { 1 }).AsTask());
        await process.DisposeAsync();
        Assert.AreEqual(1, handle.DisposeCount);
        await AssertFailedStateAsync(process);
    }

    [TestMethod]
    public async Task StartAsync_TwoConcurrentCalls_AdmitsOnlyOne()
    {
        var handle = new ControlledPtyHandle();
        var creations = 0;
        await using var process = Create(_ =>
        {
            Interlocked.Increment(ref creations);
            return handle;
        });
        var first = Task.Run(() => process.StartAsync());
        await handle.StartEntered.Task.WaitAsync(Watchdog);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => Task.Run(() => process.StartAsync()));
        handle.ReleaseStart.TrySetResult();
        await first.WaitAsync(Watchdog);
        Assert.AreEqual(1, creations);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StartAsync_ResizeDuringStartup_AppliesBeforePublishing(bool resizeFails)
    {
        var failure = new IOException("resize failed");
        var handle = new ControlledPtyHandle { ResizeError = resizeFails ? failure : null };
        await using var process = Create(_ => handle);
        var start = process.StartAsync();
        await handle.StartEntered.Task.WaitAsync(Watchdog);
        Assert.AreEqual(-1, process.ProcessId);
        await process.ResizeAsync(101, 37);
        handle.ReleaseStart.TrySetResult();

        if (resizeFails)
        {
            Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => start.WaitAsync(Watchdog)));
            Assert.AreSame(failure, await Assert.ThrowsExactlyAsync<IOException>(() => process.ReadOutputAsync().AsTask()));
            await AssertFailedStateAsync(process);
            Assert.AreEqual(1, handle.DisposeCount);
        }
        else
        {
            await start.WaitAsync(Watchdog);
            Assert.IsTrue(process.HasStarted);
            Assert.AreEqual((101, 37), handle.LastSize);
            await process.ResizeAsync(111, 41);
            Assert.AreEqual((111, 41), handle.LastSize);
        }
    }

    [TestMethod]
    public async Task ResizeAsync_UnchangedDimensions_DoesNotSendToStartedProcess()
    {
        var handle = new ControlledPtyHandle();
        handle.ReleaseStart.TrySetResult();
        await using var process = Create(_ => handle);
        await process.StartAsync();

        await process.ResizeAsync(80, 24);
        Assert.AreEqual(0, handle.ResizeCount);

        await process.ResizeAsync(101, 37);
        Assert.AreEqual(1, handle.ResizeCount);
        Assert.AreEqual((101, 37), handle.LastSize);

        await process.ResizeAsync(101, 37);
        Assert.AreEqual(1, handle.ResizeCount);
    }

#pragma warning disable HEX1B_UNIX_PTY_STARTUP
    [TestMethod]
    public async Task UnixPtyStartupTimeout_DefaultAndAcceptedValues_AreSharedAndForwarded()
    {
        var options = new Hex1bTerminalProcessOptions();
        await using var defaults = new Hex1bTerminalChildProcess("fake");
        Assert.AreEqual(TimeSpan.FromSeconds(10), options.UnixPtyStartupTimeout);
        Assert.AreEqual(options.UnixPtyStartupTimeout, defaults.UnixPtyStartupTimeout);

        foreach (var value in new[] { TimeSpan.FromTicks(1), TimeSpan.FromSeconds(31), TimeSpan.MaxValue, Timeout.InfiniteTimeSpan })
        {
            options.UnixPtyStartupTimeout = value;
            TimeSpan? received = null;
            var handle = new ControlledPtyHandle();
            handle.ReleaseStart.TrySetResult();
            await using var process = new Hex1bTerminalChildProcess("fake", [], null, null, false, 80, 24, timeout =>
            {
                received = timeout;
                return handle;
            }) { UnixPtyStartupTimeout = value };

            await process.StartAsync();
            Assert.AreEqual(value, options.UnixPtyStartupTimeout);
            Assert.AreEqual(value, process.UnixPtyStartupTimeout);
            Assert.AreEqual(value, received);
        }
    }

    [TestMethod]
    [DataRow(0L)]
    [DataRow(-1L)]
    [DataRow(-10001L)]
    [DataRow(long.MinValue)]
    public void UnixPtyStartupTimeout_InvalidValue_IsRejectedByBothProperties(long ticks)
    {
        var value = TimeSpan.FromTicks(ticks);
        var options = new Hex1bTerminalProcessOptions();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => options.UnixPtyStartupTimeout = value);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new Hex1bTerminalChildProcess("fake") { UnixPtyStartupTimeout = value });
    }

    [TestMethod]
    public async Task WithPtyProcess_TimeoutOption_IsSnapshottedBeforeBuild()
    {
        Hex1bTerminalProcessOptions? captured = null;
        var builder = Hex1bTerminal.CreateBuilder().WithPtyProcess(options =>
        {
            captured = options;
            options.FileName = "fake";
            options.UnixPtyStartupTimeout = TimeSpan.FromSeconds(19);
        }).WithHeadless();
        captured!.UnixPtyStartupTimeout = TimeSpan.FromSeconds(41);

        await using var terminal = builder.Build();
        var process = TestSeq.IsType<Hex1bTerminalChildProcess>(terminal.Workload);
        Assert.AreEqual(TimeSpan.FromSeconds(19), process.UnixPtyStartupTimeout);
    }
#pragma warning restore HEX1B_UNIX_PTY_STARTUP

    [TestMethod]
    public void UnixPtyStartupTimeout_PublicProperties_AreExperimentalWithExpectedMutability()
    {
        foreach (var type in new[] { typeof(Hex1bTerminalChildProcess), typeof(Hex1bTerminalProcessOptions) })
        {
            var property = type.GetProperty("UnixPtyStartupTimeout")!;
            Assert.AreEqual(typeof(TimeSpan), property.PropertyType);
            Assert.AreEqual("HEX1B_UNIX_PTY_STARTUP", property.GetCustomAttribute<ExperimentalAttribute>()!.DiagnosticId);
            Assert.IsNull(type.GetCustomAttribute<ExperimentalAttribute>());
            var initOnly = property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
                .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit));
            Assert.AreEqual(type == typeof(Hex1bTerminalChildProcess), initOnly);
        }
    }

    private static Hex1bTerminalChildProcess Create(Func<TimeSpan, IPtyHandle> factory)
        => new("fake", [], null, null, false, 80, 24, factory);

    private static async Task AssertFailedStateAsync(Hex1bTerminalChildProcess process)
    {
        Assert.IsFalse(process.HasStarted);
        Assert.IsFalse(process.HasExited);
        Assert.AreEqual(-1, process.ProcessId);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => process.WaitForExitAsync());
    }

    private sealed class ControlledPtyHandle : IPtyHandle
    {
        public TaskCompletionSource StartEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseStart { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisposalEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Exception? StartupError { get; init; }
        public Exception? ResizeError { get; init; }
        public Exception? KillError { get; init; }
        public Exception? DisposalError { get; init; }
        public bool HoldDisposal { get; init; }
        public bool IgnoreCancellation { get; init; }
        public bool CancelIo { get; init; }
        public int DisposeCount { get; private set; }
        public int WaitCount { get; private set; }
        public int KillSignal { get; private set; }
        public int WriteCount { get; private set; }
        public int ResizeCount { get; private set; }
        public (int, int) LastSize { get; private set; }
        public int ProcessId { get; private set; } = -1;

        public async Task StartAsync(string fileName, string[] arguments, string? workingDirectory,
            Dictionary<string, string> environment, int width, int height, CancellationToken ct)
        {
            ProcessId = 1234;
            StartEntered.TrySetResult();
            await ReleaseStart.Task.WaitAsync(IgnoreCancellation ? CancellationToken.None : ct);
            if (StartupError is not null)
                throw StartupError;
        }

        public ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
            => CancelIo ? ValueTask.FromException<ReadOnlyMemory<byte>>(new OperationCanceledException(ct))
                : ValueTask.FromResult<ReadOnlyMemory<byte>>(new byte[] { 1 });

        public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            WriteCount++;
            return CancelIo ? ValueTask.FromException(new OperationCanceledException(ct)) : ValueTask.CompletedTask;
        }

        public void Resize(int width, int height)
        {
            ResizeCount++;
            LastSize = (width, height);
            if (ResizeError is not null)
                throw ResizeError;
        }

        public void Kill(int signal)
        {
            KillSignal = signal;
            if (KillError is not null)
                throw KillError;
        }

        public Task<int> WaitForExitAsync(CancellationToken ct)
        {
            WaitCount++;
            return Task.FromResult(137);
        }

        public async ValueTask DisposeAsync()
        {
            DisposeCount++;
            DisposalEntered.TrySetResult();
            if (HoldDisposal)
                await ReleaseDisposal.Task;
            ProcessId = -1;
            if (DisposalError is not null)
                throw DisposalError;
        }
    }
}
