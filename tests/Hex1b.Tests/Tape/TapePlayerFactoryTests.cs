using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Layout;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class TapePlayerFactoryTests
{
    [TestMethod]
    public async Task Play_ConfiguredApp_ReceivesInputAndPreservesFinalSnapshot()
    {
        var clicks = 0;
        var tape = new TapeParser().Parse("Wait+Screen /Count: 0/ Enter Wait+Screen /Count: 1/");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var result = await new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
        {
            TerminalFactory = builder => builder.WithDimensions(40, 10)
                .WithHex1bApp(context => context.Button($"Count: {clicks}").OnClick(_ => { clicks++; })).Build()
        }, cancellation.Token);

        Assert.AreEqual(1, clicks);
        Assert.IsTrue(result.FinalSnapshot.ContainsText("Count: 1"));
        Assert.AreEqual(new Size(40, 10), result.TerminalSize);
        Assert.IsNull(result.ProcessExitCode);
    }

    [TestMethod]
    public async Task Validate_ConfiguredApp_DoesNotInvokeFactoryBuildOrRunTerminal()
    {
        var configured = 0;
        var built = 0;
        var rendered = 0;

        var result = await new TapePlayer().ValidateAsync(new TapeParser().Parse("Wait+Screen /Ready/"),
            new TapePlaybackOptions
            {
                TerminalFactory = builder =>
                {
                    configured++;
                    return builder.WithHex1bApp(_ => { built++; }, context =>
                    {
                        rendered++;
                        return context.Text("Ready");
                    }).Build();
                }
            });

        Assert.IsTrue(result.CanExecute);
        Assert.AreEqual(0, configured);
        Assert.AreEqual(0, built);
        Assert.AreEqual(0, rendered);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "TAPE_FACTORY_VALIDATION"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Play_ConfiguredDimensionsAndRawWorkload_CapturesFromStartupAndDisposes(bool overrideSize)
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-builder-{Guid.NewGuid():N}"));
        try
        {
            var castPath = Path.Combine(directory.FullName, "session.cast");
            var goldenPath = Path.Combine(directory.FullName, "session.txt");
            var workload = new TrackingWorkload(castPath);
            var tape = new TapeParser().Parse("Wait+Screen /Owned ready/ Type 'abc' Wait+Screen /abc/");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            var factoryCalls = 0;
            using var result = await new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
                {
                    TerminalFactory = builder =>
                    {
                        factoryCalls++;
                        return builder.WithWorkload(workload).WithDimensions(101, 33).Build();
                    },
                    TerminalSize = overrideSize ? new Size(90, 28) : null,
                    Capture = new TapeCaptureOptions { AsciinemaPath = castPath, GoldenTextPath = goldenPath }
                }, cancellation.Token);

            var size = overrideSize ? new Size(90, 28) : new Size(101, 33);
            Assert.AreEqual(1, factoryCalls);
            Assert.AreEqual(size, result.TerminalSize);
            Assert.AreEqual(size, workload.Size);
            Assert.IsTrue(workload.CaptureExistedBeforeRead);
            Assert.IsTrue(workload.IsDisposed);
            Assert.IsTrue(result.FinalSnapshot.ContainsText("abc"));
            Assert.IsTrue((await File.ReadAllTextAsync(goldenPath)).Contains("Owned ready"));
            var lines = await File.ReadAllLinesAsync(castPath);
            using var header = JsonDocument.Parse(lines[0]);
            Assert.AreEqual(size.Width, header.RootElement.GetProperty("width").GetInt32());
            Assert.AreEqual(size.Height, header.RootElement.GetProperty("height").GetInt32());
            Assert.IsTrue(lines.Skip(1).Any(line => line.Contains("Owned ready")));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Play_DimensionsOnlyFactory_RetainsDefaultShell()
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var result = await new TapePlayer().PlayAsync(new TapeParser().Parse("Wait />$/"),
            new TapePlaybackOptions { TerminalFactory = builder => builder.WithDimensions(100, 30).Build() }, cancellation.Token);

        Assert.AreEqual(new Size(100, 30), result.TerminalSize);
        Assert.IsTrue(result.FinalSnapshot.ContainsText(">"));
    }

    [TestMethod]
    public async Task Play_InvalidPreflight_DoesNotBuildOrStartWorkload()
    {
        await using var workload = new TrackingWorkload();
        var tape = new TapeParser().Parse("Screenshot 'unsupported.png'");
        var configured = 0;

        await Assert.ThrowsAsync<TapeValidationException>(() => new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
        {
            TerminalFactory = builder =>
            {
                configured++;
                return builder.WithWorkload(workload).Build();
            }
        }));

        Assert.AreEqual(1, configured);
        Assert.AreEqual(default, workload.Size);
        Assert.IsFalse(workload.ReadStarted.Task.IsCompleted);
        Assert.IsFalse(workload.IsDisposed);
    }

    [TestMethod]
    [DataRow("Env TAPE_VALUE 'value'")]
    [DataRow("Set Shell 'bash'")]
    [DataRow("Require 'dotnet'")]
    public async Task Play_CustomWorkload_RejectsDefaultShellCommands(string source)
    {
        var error = await Assert.ThrowsAsync<TapeValidationException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse(source), new TapePlaybackOptions
            {
                TerminalFactory = builder => builder.WithHex1bApp(context => context.Text("Ready")).Build()
            }));

        Assert.IsTrue(error.Diagnostics.Any(d => d.Severity == TapeDiagnosticSeverity.Error));
    }

    [TestMethod]
    public async Task Validate_LaunchOptionsWithoutDefaultShell_RejectsRatherThanIgnoring()
    {
        var player = new TapePlayer();
        var tape = new TapeParser().Parse("");
        var options = new TapePlaybackOptions
        {
            Environment = new Dictionary<string, string> { ["TAPE_VALUE"] = "value" },
            TerminalFactory = builder => builder.WithHex1bApp(context => context.Text("Ready")).Build()
        };
        await Assert.ThrowsAsync<TapeValidationException>(() => player.PlayAsync(tape, options));
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        var borrowed = await player.ValidateAsync(tape, terminal, options);

        Assert.IsFalse(borrowed.CanExecute);
    }

    [TestMethod]
    public async Task Play_FactoryThrows_PropagatesOriginalException()
    {
        var failure = new InvalidOperationException("configuration failed");
        var error = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            new TapePlayer().PlayAsync(new TapeParser().Parse(""),
                new TapePlaybackOptions { TerminalFactory = _ => throw failure }));

        Assert.AreSame(failure, error);
    }

    [TestMethod]
    public async Task Play_AlreadyCancelled_DoesNotInvokeFactory()
    {
        var configured = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse(""), new TapePlaybackOptions
            {
                TerminalFactory = builder => { configured = true; return builder.Build(); }
            }, cancellation.Token));

        Assert.IsFalse(configured);
    }

    [TestMethod]
    public async Task Play_CancelledDuringSleep_DisposesOwnedWorkload()
    {
        var workload = new TrackingWorkload();
        using var cancellation = new CancellationTokenSource();
        var play = new TapePlayer().PlayAsync(new TapeParser().Parse("Sleep 60m"),
            new TapePlaybackOptions { TerminalFactory = builder => builder.WithWorkload(workload).Build() }, cancellation.Token);
        await workload.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => play.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(workload.IsDisposed);
    }

    [TestMethod]
    public async Task Play_WaitFails_DisposesOwnedWorkload()
    {
        var workload = new TrackingWorkload();

        await Assert.ThrowsAsync<TapePlaybackException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse("Wait@30ms /never matches/"),
            new TapePlaybackOptions { TerminalFactory = builder => builder.WithWorkload(workload).Build() }));

        Assert.IsTrue(workload.IsDisposed);
    }

    [TestMethod]
    [DataRow("Sleep 60m")]
    [DataRow("Wait@60m /never matches/")]
    [DataRow("Type 'blocked input'")]
    public async Task Play_ProcessStartupFails_InterruptsPendingSequence(string source)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var error = await Assert.ThrowsAsync<TapePlaybackException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse(source), new TapePlaybackOptions
            {
                TerminalFactory = builder => builder.WithProcess("hex1b-missing-tape-program-8675309").Build()
            }, cancellation.Token));

        Assert.IsNotInstanceOfType<WaitUntilTimeoutException>(error.InnerException);
    }

    [TestMethod]
    public async Task Play_OutputPumpFails_DisposesOwnedWorkload()
    {
        var workload = new TrackingWorkload { FailRead = true };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var error = await Assert.ThrowsAsync<TapePlaybackException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse("Sleep 60m"),
            new TapePlaybackOptions { TerminalFactory = builder => builder.WithWorkload(workload).Build() }, cancellation.Token));

        Assert.IsInstanceOfType<InvalidOperationException>(error.InnerException);
        Assert.IsTrue(workload.IsDisposed);
    }

    [TestMethod]
    public async Task Play_CustomClock_UsesItForPreparationAndElapsedTime()
    {
        var clock = new FakeTimeProvider();
        var workload = new TrackingWorkload();
        var options = new TapeParserOptions();
        options.SyntaxExtensions["AdvanceClock"] = _ => context =>
        {
            Assert.AreSame(clock, context.SequenceOptions.TimeProvider);
            return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions)
                .WaitUntil(_ => { clock.Advance(TimeSpan.FromSeconds(2)); return true; }, TimeSpan.FromSeconds(5)));
        };
        var tape = new TapeParser(options).Parse("AdvanceClock");

        using var result = await new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
        {
            TerminalFactory = builder => builder.WithWorkload(workload).WithTimeProvider(clock).Build()
        });

        Assert.AreEqual(TimeSpan.FromSeconds(2), result.Elapsed);
        Assert.IsTrue(workload.IsDisposed);
    }

    [TestMethod]
    public async Task Play_WorkloadFailsDuringSequence_CancelsPendingWaitAndPreservesFailure()
    {
        var workload = new TrackingWorkload();
        var stepStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new IOException("asynchronous startup failure");
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Pending"] = _ => context => TapeCommandResult.Accept(
            new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions)
                .WaitUntil(_ => { stepStarted.TrySetResult(); return false; }, TimeSpan.FromHours(1)));
        var tape = new TapeParser(options).Parse("Pending");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var error = await Assert.ThrowsAsync<TapePlaybackException>(() => new TapePlayer().PlayAsync(tape,
            new TapePlaybackOptions
            {
                TerminalFactory = builder =>
                {
                    builder.SetWorkloadFactory(_ => new Hex1bTerminalBuildContext(workload, async ct =>
                    {
                        await stepStarted.Task.WaitAsync(ct);
                        throw failure;
                    }));
                    return builder.Build();
                }
            }, cancellation.Token));

        Assert.AreSame(failure, error.InnerException);
        Assert.IsTrue(workload.IsDisposed);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Play_CleanupFails_PreservesCaptureAndCommandContext(bool commandFails)
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-cleanup-{Guid.NewGuid():N}"));
        try
        {
            var path = Path.Combine(directory.FullName, "session.cast");
            var workload = new TrackingWorkload { FailDispose = true };
            var source = "Wait+Screen /Owned ready/";
            if (commandFails)
                source += "\nWait@30ms /never matches/";
            var tape = new TapeParser().Parse(source, "cleanup.tape");

            var error = await Assert.ThrowsAsync<TapePlaybackException>(() => new TapePlayer().PlayAsync(
                tape, new TapePlaybackOptions
                {
                    TerminalFactory = builder => builder.WithWorkload(workload).Build(),
                    Capture = new TapeCaptureOptions { AsciinemaPath = path }
                }));

            Assert.AreEqual(1, error.CompletedCommandCount);
            Assert.IsTrue(error.TerminalText.Contains("Owned ready"));
            Assert.AreEqual(path, TestSeq.Single(error.PartialArtifacts).Path);
            Assert.IsTrue(workload.IsDisposed);
            if (commandFails)
            {
                Assert.AreSame(tape.Commands[1], error.Command);
                Assert.IsInstanceOfType<AggregateException>(error.InnerException);
            }
            else
            {
                Assert.IsNull(error.Command);
                Assert.IsInstanceOfType<IOException>(error.InnerException);
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Play_CancelledAndCleanupFails_PreservesCancellation()
    {
        var workload = new TrackingWorkload { FailDispose = true };
        using var cancellation = new CancellationTokenSource();
        var play = new TapePlayer().PlayAsync(new TapeParser().Parse("Sleep 60m"),
            new TapePlaybackOptions { TerminalFactory = builder => builder.WithWorkload(workload).Build() }, cancellation.Token);
        await workload.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => play.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsTrue(error.CancellationToken.IsCancellationRequested);
        Assert.IsInstanceOfType<AggregateException>(error.InnerException);
        Assert.IsTrue(workload.IsDisposed);
    }

    [TestMethod]
    public async Task Play_ExistingTerminal_IgnoresFactoryAndRetainsBorrowedOwnership()
    {
        var workload = new TrackingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var options = new TapePlaybackOptions { TerminalFactory = _ => throw new InvalidOperationException("must not be invoked") };
        var player = new TapePlayer();
        var tape = new TapeParser().Parse("Wait+Screen /Owned ready/ Type 'borrowed' Wait+Screen /borrowed/");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var validation = await player.ValidateAsync(tape, terminal, options, cancellation.Token);
        Assert.IsTrue(validation.CanExecute);
        using (var result = await player.PlayAsync(tape, terminal, options, cancellation.Token))
            Assert.IsTrue(result.FinalSnapshot.ContainsText("borrowed"));

        Assert.IsFalse(workload.IsDisposed);
        using var next = await player.PlayAsync(new TapeParser().Parse("Type 'again' Wait+Screen /again/"),
            terminal, options, cancellation.Token);
        Assert.IsTrue(next.FinalSnapshot.ContainsText("again"));
    }

    [TestMethod]
    public async Task Play_FactoryThrowsAfterBuild_DisposesUnstartedTerminal()
    {
        var workload = new TrackingWorkload();
        var failure = new IOException("factory failed after construction");

        var error = await Assert.ThrowsExactlyAsync<IOException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse(""), new TapePlaybackOptions
            {
                TerminalFactory = builder =>
                {
                    builder.WithWorkload(workload).Build();
                    throw failure;
                }
            }));

        Assert.AreSame(failure, error);
        Assert.IsTrue(workload.IsDisposed);
        Assert.IsFalse(workload.ReadStarted.Task.IsCompleted);
    }

    [TestMethod]
    public async Task Play_FactoryBuildsTwice_RejectsAndDisposesFirstTerminal()
    {
        var workload = new TrackingWorkload();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse(""), new TapePlaybackOptions
            {
                TerminalFactory = builder =>
                {
                    builder.WithWorkload(workload).Build();
                    return builder.Build();
                }
            }));

        Assert.IsTrue(workload.IsDisposed);
        Assert.IsFalse(workload.ReadStarted.Task.IsCompleted);
    }

    [TestMethod]
    public async Task Play_FactoryReturnsNull_RejectsAndDisposesConstructedTerminal()
    {
        var workload = new TrackingWorkload();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse(""), new TapePlaybackOptions
            {
                TerminalFactory = builder =>
                {
                    builder.WithWorkload(workload).Build();
                    return null!;
                }
            }));

        Assert.IsTrue(workload.IsDisposed);
        Assert.IsFalse(workload.ReadStarted.Task.IsCompleted);
    }

    [TestMethod]
    public async Task Play_FactoryReturnsUnrelatedTerminal_RejectsWithoutTakingItsOwnership()
    {
        var workload = new TrackingWorkload();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse(""), new TapePlaybackOptions { TerminalFactory = _ => terminal }));

        Assert.IsFalse(workload.IsDisposed);
    }

    [TestMethod]
    public async Task Play_FactoryChangesSourceAndSyntax_UsesSingleResolvedSnapshot()
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-factory-source-{Guid.NewGuid():N}"));
        try
        {
            var path = Path.Combine(directory.FullName, "part.tape");
            await File.WriteAllTextAsync(path, "Type 'original'");
            var options = new TapeParserOptions();
            var workload = new TrackingWorkload();
            var preparations = 0;
            options.SyntaxExtensions["Observe"] = _ => context =>
            {
                preparations++;
                return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
            };
            var tape = new TapeParser(options).Parse("Observe Source 'part.tape' Wait+Screen /original/");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            using var result = await new TapePlayer().PlayAsync(tape, new TapePlaybackOptions
            {
                WorkingDirectory = directory.FullName,
                TerminalFactory = builder =>
                {
                    File.WriteAllText(path, "Type 'changed'");
                    options.SyntaxExtensions["Type"] = parse =>
                    {
                        parse.Reader.Read();
                        return _ => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().Type("replaced"));
                    };
                    return builder.WithWorkload(workload).Build();
                }
            }, cancellation.Token);

            Assert.AreEqual(1, preparations);
            Assert.IsTrue(result.FinalSnapshot.ContainsText("original"));
            Assert.IsFalse(result.FinalSnapshot.ContainsText("changed"));
            Assert.IsFalse(result.FinalSnapshot.ContainsText("replaced"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Play_LateCallbackError_DoesNotBuildStartResizeOrCaptureOwnedWorkload()
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-rejected-{Guid.NewGuid():N}"));
        try
        {
            await using var workload = new TrackingWorkload();
            var options = new TapeParserOptions();
            var prepared = new List<string>();
            options.SyntaxExtensions["Reject"] = _ => _ =>
            {
                prepared.Add("Reject");
                return TapeCommandResult.Reject("Playback is disabled.");
            };
            options.SyntaxExtensions["AfterReject"] = _ => context =>
            {
                prepared.Add("AfterReject");
                return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
            };
            var tape = new TapeParser(options).Parse("Type 'never sent' Reject AfterReject");
            var built = false;
            var factoryCalls = 0;
            var path = Path.Combine(directory.FullName, "blocked.cast");
            var golden = Path.Combine(directory.FullName, "blocked.txt");
            var playback = new TapePlaybackOptions
            {
                TerminalSize = new Size(30, 6),
                Capture = new TapeCaptureOptions { AsciinemaPath = path, GoldenTextPath = golden },
                TerminalFactory = builder =>
                {
                    factoryCalls++;
                    var terminal = builder.WithWorkload(workload).Build();
                    built = true;
                    return terminal;
                }
            };
            var player = new TapePlayer();
            var validation = await player.ValidateAsync(tape, playback);
            Assert.IsFalse(validation.CanExecute);
            TestSeq.AreEqual(["Reject", "AfterReject"], prepared);
            Assert.AreEqual(0, factoryCalls);
            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(golden));
            prepared.Clear();

            var error = await Assert.ThrowsAsync<TapeValidationException>(() => player.PlayAsync(tape, playback));

            TestSeq.AreEqual(["Reject", "AfterReject"], prepared);
            Assert.AreEqual("Playback is disabled.", TestSeq.Single(error.Diagnostics.Where(d => d.Code == "TAPE_COMMAND")).Message);
            Assert.IsFalse(built);
            Assert.IsFalse(workload.ReadStarted.Task.IsCompleted);
            Assert.AreEqual(default, workload.Size);
            Assert.AreEqual(0, workload.InputBytes);
            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(golden));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class TrackingWorkload(string? capturePath = null) : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private int _reads;
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsDisposed { get; private set; }
        public bool CaptureExistedBeforeRead { get; private set; }
        public bool FailRead { get; init; }
        public bool FailDispose { get; init; }
        public Size Size { get; private set; }
        public int InputBytes { get; private set; }
        public event Action? Disconnected;

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            if (Interlocked.Increment(ref _reads) == 1)
            {
                CaptureExistedBeforeRead = capturePath is null || File.Exists(capturePath);
                ReadStarted.TrySetResult();
                if (FailRead)
                    throw new IOException("simulated output failure");
                return Encoding.UTF8.GetBytes("Owned ready");
            }
            if (await _output.Reader.WaitToReadAsync(ct) && _output.Reader.TryRead(out var data))
                return data;
            return ReadOnlyMemory<byte>.Empty;
        }

        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            InputBytes += data.Length;
            return _output.Writer.WriteAsync(data.ToArray(), ct);
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        {
            Size = new(width, height);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            _output.Writer.TryComplete();
            Disconnected?.Invoke();
            return FailDispose
                ? ValueTask.FromException(new IOException("simulated disposal failure"))
                : ValueTask.CompletedTask;
        }
    }
}
