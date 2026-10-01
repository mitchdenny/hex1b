using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class UnixPtyStartupDeadlineTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task StartAsync_DeadlineExpires_ReportsCheckpointAndCleansUp(bool ready)
    {
        var native = new FakeStartupInterop { Ready = ready };
        await using var handle = new UnixPtyHandle(TimeSpan.FromSeconds(3), native.Clock, native);
        var error = await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            handle.StartAsync("/bin/pwd", ["private-argument"], "/test-cwd",
                new() { ["SECRET"] = "private-environment" }, 80, 24, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Contains("3 seconds", error.Message);
        Assert.Contains("elapsed", error.Message);
        Assert.Contains(Path.GetFullPath("/bin/pwd"), error.Message);
        Assert.Contains("/test-cwd", error.Message);
        Assert.Contains("UnixPtyStartupTimeout", error.Message);
        Assert.Contains("reaped", error.Message);
        Assert.Contains(ready ? "exec completion was not confirmed" : "No pre-exec confirmation", error.Message);
        Assert.DoesNotContain("private-argument", error.Message);
        Assert.DoesNotContain("private-environment", error.Message);
        Assert.DoesNotContain("target was not executed", error.Message);
        Assert.AreEqual(1, native.AbortCount);
        Assert.AreEqual(-1, handle.ProcessId);
        Assert.IsTrue(native.PollIntervals.All(value => value > 0 && value <= 50));
    }

    [TestMethod]
    public async Task StartAsync_InfiniteTimeout_DoesNotExpireButCanBeCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var native = new FakeStartupInterop();
        native.OnPoll = () =>
        {
            native.Clock.Advance(TimeSpan.FromDays(365));
            if (native.PollIntervals.Count == 2)
                cancellation.Cancel();
        };
        await using var handle = new UnixPtyHandle(Timeout.InfiniteTimeSpan, native.Clock, native);
        var error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(2, native.PollIntervals.Count);
        Assert.AreEqual(1, native.AbortCount);
    }

    [TestMethod]
    [DataRow(1L)]
    [DataRow(40000000000000L)]
    [DataRow(long.MaxValue)]
    public async Task StartAsync_PositiveTimeout_ClampsNativePollWithoutOverflow(long ticks)
    {
        var timeout = TimeSpan.FromTicks(ticks);
        var native = new FakeStartupInterop { Result = 0 };
        if (ticks == 1)
            native.AdvanceOnPoll = false;
        await using var handle = new UnixPtyHandle(timeout, native.Clock, native);
        await handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(200, handle.ProcessId);
        Assert.AreEqual(ticks == 1 ? 1 : 50, TestSeq.Single(native.PollIntervals));
        Assert.AreEqual(1, native.CloseCount);
        Assert.AreEqual(0, native.AbortCount);
    }

    [TestMethod]
    public async Task StartAsync_CanceledBeforeStart_DoesNotBeginNativeLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var native = new FakeStartupInterop();
        await using var handle = new UnixPtyHandle(TimeSpan.FromSeconds(10), native.Clock, native);
        var error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, cancellation.Token));
        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(0, native.BeginCount);
    }

    [TestMethod]
    public async Task StartAsync_CancellationAndDeadline_CancellationWins()
    {
        using var cancellation = new CancellationTokenSource();
        var native = new FakeStartupInterop();
        native.OnPoll = cancellation.Cancel;
        await using var handle = new UnixPtyHandle(TimeSpan.FromMilliseconds(1), native.Clock, native);
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, cancellation.Token)
                .WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.AreEqual(1, native.AbortCount);
    }

    [TestMethod]
    public async Task StartAsync_NativeFailureAtDeadline_PreservesNativeCause()
    {
        var native = new FakeStartupInterop { Result = -1, ErrorStage = 2, Error = 13 };
        await using var handle = new UnixPtyHandle(TimeSpan.FromMilliseconds(1), native.Clock, native);
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, CancellationToken.None));
        Assert.AreEqual(13, TestSeq.IsType<Win32Exception>(error.InnerException).NativeErrorCode);
        Assert.Contains("could not enter", error.Message);
        Assert.AreEqual(1, native.AbortCount);
    }

    [TestMethod]
    public async Task StartAsync_DeadlineAfterReady_DoesNotPublishProvisionalPid()
    {
        var native = new FakeStartupInterop { Result = 0, Ready = true };
        await using var handle = new UnixPtyHandle(TimeSpan.FromMilliseconds(1), native.Clock, native);
        await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
            handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, CancellationToken.None));
        Assert.AreEqual(-1, handle.ProcessId);
        Assert.AreEqual(0, native.CloseCount);
        Assert.AreEqual(1, native.AbortCount);
    }

    [TestMethod]
    public async Task StartAsync_CleanupFailure_PreservesTimeoutWithoutClaimingCleanupSucceeded()
    {
        var cleanupFailure = new IOException("controlled cleanup failure");
        var native = new FakeStartupInterop { AbortFailure = cleanupFailure };
        await using var handle = new UnixPtyHandle(TimeSpan.FromMilliseconds(1), native.Clock, native);
        var error = await Assert.ThrowsExactlyAsync<AggregateException>(() =>
            handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, CancellationToken.None));
        var timeout = TestSeq.IsType<TimeoutException>(error.InnerExceptions[0]);
        Assert.AreSame(cleanupFailure, error.InnerExceptions[1]);
        Assert.DoesNotContain("reaped", timeout.Message);
    }

    [TestMethod]
    public async Task StartAsync_MissingCapability_DoesNotBeginLaunch()
    {
        var native = new FakeStartupInterop { ValidationFailure = new InvalidOperationException("missing startup export") };
        await using var handle = new UnixPtyHandle(TimeSpan.FromSeconds(10), native.Clock, native);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, CancellationToken.None));
        Assert.AreEqual(0, native.BeginCount);
        Assert.AreEqual(0, native.AbortCount);
    }

    [TestMethod]
    public async Task DisposeAsync_PendingStartup_CancelsAndReapsBeforeCompleting()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var native = new FakeStartupInterop();
        native.OnPoll = () =>
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("Test did not release the poll barrier.");
        };
        var handle = new UnixPtyHandle(Timeout.InfiniteTimeSpan, native.Clock, native);
        var start = handle.StartAsync("/bin/pwd", [], "/test-cwd", new(), 80, 24, CancellationToken.None);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposal = handle.DisposeAsync().AsTask();
            release.Set();
            await disposal.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => start);
            Assert.AreEqual(1, native.AbortCount);
            Assert.AreEqual(-1, handle.ProcessId);
        }
        finally
        {
            release.Set();
            await handle.DisposeAsync();
        }
    }

    [TestMethod]
    public void StartupState_HasNativeLayout()
    {
        Assert.AreEqual(16, Marshal.SizeOf<UnixPtyStartupState>());
        Assert.AreEqual((nint)0, Marshal.OffsetOf<UnixPtyStartupState>(nameof(UnixPtyStartupState.Ready)));
        Assert.AreEqual((nint)4, Marshal.OffsetOf<UnixPtyStartupState>(nameof(UnixPtyStartupState.BytesRead)));
        Assert.AreEqual((nint)8, Marshal.OffsetOf<UnixPtyStartupState>(nameof(UnixPtyStartupState.RecordTag)));
        Assert.AreEqual((nint)12, Marshal.OffsetOf<UnixPtyStartupState>(nameof(UnixPtyStartupState.RecordError)));
    }

    private sealed class FakeStartupInterop : IUnixPtyStartupInterop
    {
        public FakeTimeProvider Clock { get; } = new();
        public List<int> PollIntervals { get; } = [];
        public int Result { get; set; } = 1;
        public int ErrorStage { get; set; }
        public int Error { get; set; }
        public bool Ready { get; set; }
        public bool AdvanceOnPoll { get; set; } = true;
        public Action? OnPoll { get; set; }
        public Exception? AbortFailure { get; set; }
        public Exception? ValidationFailure { get; set; }
        public int AbortCount { get; private set; }
        public int BeginCount { get; private set; }
        public int CloseCount { get; private set; }

        public void ValidateLibrary()
        {
            if (ValidationFailure is not null)
                throw ValidationFailure;
        }

        public UnixPtyStartupHandles Begin(string executable, string[] arguments, string workingDirectory,
            string[] environment, int width, int height)
        {
            BeginCount++;
            return new(100, 200, 300);
        }

        public int Poll(int startupFd, int timeoutMilliseconds, ref UnixPtyStartupState state, out int stage, out int error)
        {
            Assert.AreEqual(300, startupFd);
            PollIntervals.Add(timeoutMilliseconds);
            if (AdvanceOnPoll)
                Clock.Advance(TimeSpan.FromMilliseconds(timeoutMilliseconds));
            OnPoll?.Invoke();
            state.Ready = Ready ? 1 : 0;
            stage = ErrorStage;
            error = Error;
            return Result;
        }

        public void CloseStartup(int startupFd)
        {
            Assert.AreEqual(300, startupFd);
            CloseCount++;
        }

        public void Abort(UnixPtyStartupHandles handles)
        {
            Assert.AreEqual(200, handles.ChildPid);
            Assert.AreEqual(100, handles.MasterFd);
            AbortCount++;
            if (AbortFailure is not null)
                throw AbortFailure;
        }
    }
}
