using System.Text;
using System.Threading.Channels;
using Hex1b.Automation;
using Hex1b.Layout;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class TapePlayerTests
{
    [TestMethod]
    public async Task Play_UnsupportedLaterCommand_HasNoInputResizeOrFileEffects()
    {
        using var files = new CaptureDirectory();
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var originalResizeCount = workload.ResizeCount;
        var tape = new TapeParser().Parse("Type 'never sent' Output 'movie.mp4'");
        var options = new TapePlaybackOptions
        {
            TerminalSize = new Size(30, 6),
            Capture = new TapeCaptureOptions { AsciinemaPath = files.File("blocked.cast") }
        };

        await Assert.ThrowsAsync<TapeValidationException>(
            () => new TapePlayer().PlayAsync(tape, terminal, options));

        Assert.AreEqual("", workload.Input.ToString());
        Assert.AreEqual(originalResizeCount, workload.ResizeCount);
        Assert.IsFalse(System.IO.File.Exists(files.File("blocked.cast")));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(20, snapshot.Width);
    }

    [TestMethod]
    public async Task Play_TypeWaitAndResultDisposal_PreservesBorrowedTerminal()
    {
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        terminal.Start();
        var tape = new TapeParser().Parse("Set TypingSpeed 0 Type 'hello' Wait+Screen /hello/");

        using (var result = await new TapePlayer().PlayAsync(tape, terminal))
        {
            Assert.IsTrue(result.FinalSnapshot.ContainsText("hello"));
            Assert.AreEqual(3, result.CompletedCommandCount);
            Assert.IsNull(result.ProcessExitCode);
        }
        using var after = await new Hex1bTerminalInputSequenceBuilder().Type("!").Build().ApplyAsync(terminal);

        Assert.AreEqual("hello!", workload.Input.ToString());
        Assert.IsFalse(workload.Disposed);
    }

    [TestMethod]
    public async Task Play_EditedDocument_ExecutesCurrentCommandOrder()
    {
        var parser = new TapeParser();
        var tape = parser.Parse("Screenshot 'unused.png' Set TypingSpeed 0 Type 'original' Type 'tail'", "edit.tape");
        tape.Commands.RemoveAt(0);
        tape.Commands[1] = parser.Parse("Type 'edited'").Commands[0];
        tape.Commands.Insert(2, parser.Parse("Type '-'").Commands[0]);
        tape.Commands.Add(parser.Parse("Wait+Screen /edited-tail/").Commands[0]);
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        using var result = await new TapePlayer().PlayAsync(tape, terminal);

        Assert.IsTrue(result.FinalSnapshot.ContainsText("edited-tail"));
        Assert.AreEqual("edited-tail", workload.Input.ToString());
        Assert.AreEqual(5, result.CompletedCommandCount);
        Assert.AreEqual("edit.tape", tape.SourceName);
    }

    [TestMethod]
    public async Task Play_DocumentEditedDuringPreparation_UsesSnapshotUntilNextPlayback()
    {
        TapeDocument tape = null!;
        TapeCommand replacement = null!;
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Mutate"] = _ => context =>
        {
            tape.Commands.Clear();
            tape.Commands.Add(replacement);
            return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
        };
        var parser = new TapeParser(options);
        tape = parser.Parse("Set TypingSpeed 0 Mutate Type 'original' Type 'tail'");
        replacement = parser.Parse("Type 'changed'").Commands[0];
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var player = new TapePlayer();

        using var first = await player.PlayAsync(tape, terminal);

        Assert.AreEqual("originaltail", workload.Input.ToString());
        Assert.AreEqual(4, first.CompletedCommandCount);
        using var second = await player.PlayAsync(tape, terminal);
        Assert.AreEqual("originaltailchanged", workload.Input.ToString());
        Assert.AreEqual(1, second.CompletedCommandCount);
    }

    [TestMethod]
    public async Task Play_NullCommandAdded_RejectsBeforeInputOrCapture()
    {
        using var files = new CaptureDirectory();
        var tape = new TapeParser().Parse("Type 'safe'");
        tape.Commands.Add(null!);
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var player = new TapePlayer();
        var options = new TapePlaybackOptions
        {
            Capture = new TapeCaptureOptions { AsciinemaPath = files.File("blocked.cast") }
        };

        var validation = await player.ValidateAsync(tape, terminal, options);
        Assert.IsFalse(validation.CanExecute);
        Assert.AreEqual("TAPE_DOCUMENT", TestSeq.Single(validation.Diagnostics).Code);
        await Assert.ThrowsAsync<TapeValidationException>(() => player.PlayAsync(tape, terminal, options));
        Assert.AreEqual("", workload.Input.ToString());
        Assert.IsFalse(System.IO.File.Exists(files.File("blocked.cast")));

        tape.Commands.RemoveAt(1);
        using var result = await player.PlayAsync(tape, terminal);
        Assert.AreEqual("safe", workload.Input.ToString());
    }

    [TestMethod]
    public async Task Play_OverlappingInstances_RejectsAndReleasesLeaseAfterCancellation()
    {
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload, new FakeTimeProvider());
        using var cancellation = new CancellationTokenSource();
        var first = new TapePlayer().PlayAsync(new TapeParser().Parse("Sleep 60m"), terminal,
            cancellationToken: cancellation.Token);

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new TapePlayer().PlayAsync(new TapeParser().Parse("Type 'blocked'"), terminal));
        }
        finally
        {
            await cancellation.CancelAsync();
        }
        await Assert.ThrowsAsync<OperationCanceledException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
        using var result = await new TapePlayer().PlayAsync(
            new TapeParser().Parse("Set TypingSpeed 0 Type 'after'"), terminal);
        Assert.AreEqual("after", workload.Input.ToString());
    }

    [TestMethod]
    public async Task Play_RegisteredCommand_ProducesOneGoldenCheckpoint()
    {
        using var files = new CaptureDirectory();
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Custom"] = _ => context => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder()
            .WithOptions(context.SequenceOptions).Type("ab").Type("cd"));
        var tape = new TapeParser(options).Parse("Custom");

        using var result = await new TapePlayer().PlayAsync(tape, terminal, new TapePlaybackOptions
        {
            Capture = new TapeCaptureOptions { GoldenTextPath = files.File("one.txt") }
        });

        var golden = await System.IO.File.ReadAllTextAsync(files.File("one.txt"));
        Assert.AreEqual(1, golden.Split(new string('\u2500', 80), StringSplitOptions.None).Length - 1);
        Assert.AreEqual("abcd", workload.Input.ToString());
        Assert.AreEqual(1, result.CompletedCommandCount);
    }

    [TestMethod]
    public void Parse_RemovedSource_RejectsBeforeOpeningMissingInclude()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Remove("Source");

        var error = Assert.ThrowsExactly<TapeParseException>(() =>
            new TapeParser(options).Parse("Source 'missing.tape'", "restricted.tape"));

        Assert.IsTrue(error.Diagnostics.All(d => d.Stage == TapeDiagnosticStage.Parsing));
        Assert.IsTrue(error.Diagnostics.All(d => d.Span.SourceName == "restricted.tape"));
    }

    [TestMethod]
    public async Task Validate_SourceCycle_ReportsResolutionError()
    {
        using var files = new CaptureDirectory();
        await System.IO.File.WriteAllTextAsync(files.File("cycle.tape"), "Source 'cycle.tape'");
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        var result = await new TapePlayer().ValidateAsync(new TapeParser().Parse("Source 'cycle.tape'"),
            terminal, new TapePlaybackOptions { WorkingDirectory = files.Path });

        Assert.IsFalse(result.CanExecute);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Stage == TapeDiagnosticStage.Resolution &&
            d.Message.Contains("cycle", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public async Task Play_NestedSource_UsesWorkingDirectoryAndStripsIncludedOutput()
    {
        using var files = new CaptureDirectory();
        Directory.CreateDirectory(files.File("sub"));
        await System.IO.File.WriteAllTextAsync(files.File("sub/include.tape"),
            "Output 'ignored.mp4' Source 'leaf.tape'");
        await System.IO.File.WriteAllTextAsync(files.File("leaf.tape"), "Set TypingSpeed 0 Type 'leaf'");
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        using var result = await new TapePlayer().PlayAsync(new TapeParser().Parse("Source 'sub/include.tape'"),
            terminal, new TapePlaybackOptions { WorkingDirectory = files.Path });

        Assert.AreEqual("leaf", workload.Input.ToString());
        Assert.AreEqual(2, result.CompletedCommandCount);
        Assert.IsEmpty(result.Artifacts);
    }

    [TestMethod]
    public async Task Play_SourceChangedAfterResolution_ExecutesPreparedContents()
    {
        using var files = new CaptureDirectory();
        await System.IO.File.WriteAllTextAsync(files.File("include.tape"), "Set TypingSpeed 0 Type 'original'");
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var options = new TapeParserOptions();
        options.SyntaxExtensions["MutateSource"] = _ => context =>
        {
            System.IO.File.WriteAllText(files.File("include.tape"), "Type 'changed'");
            return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
        };

        using var result = await new TapePlayer().PlayAsync(
            new TapeParser(options).Parse("MutateSource Source 'include.tape' Type 'tail'"), terminal,
            new TapePlaybackOptions
            {
                WorkingDirectory = files.Path
            });

        Assert.AreEqual("originaltail", workload.Input.ToString());
        Assert.AreEqual(4, result.CompletedCommandCount);
    }

    [TestMethod]
    public async Task Validate_UnknownProgrammaticCommand_ReportsUnsupportedWithoutInput()
    {
        var tape = new TapeDocument([new CustomCommand(default)]);
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        var result = await new TapePlayer().ValidateAsync(tape, terminal);

        Assert.IsFalse(result.CanExecute);
        Assert.AreEqual("TAPE_UNSUPPORTED", TestSeq.Single(result.Diagnostics).Code);
        Assert.AreEqual("", workload.Input.ToString());
    }

    [TestMethod]
    public async Task Play_ExistingCaptureWithoutOverwrite_LeavesOriginalUntouched()
    {
        using var files = new CaptureDirectory();
        await System.IO.File.WriteAllTextAsync(files.File("existing.cast"), "original");
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        await Assert.ThrowsAsync<TapeValidationException>(() => new TapePlayer().PlayAsync(
            new TapeParser().Parse("Type 'no'"), terminal, new TapePlaybackOptions
            {
                Capture = new TapeCaptureOptions { AsciinemaPath = files.File("existing.cast") }
            }));

        Assert.AreEqual("original", await System.IO.File.ReadAllTextAsync(files.File("existing.cast")));
        Assert.AreEqual("", workload.Input.ToString());
    }

    [TestMethod]
    [DataRow("Enter@0 3", "\r\r\r")]
    [DataRow("Enter 0", "")]
    [DataRow("Enter 0001", "\r")]
    [DataRow("Enter 1.5", "\r")]
    [DataRow("Enter 9223372036854775808", "\r")]
    [DataRow("Enter@1..2ms", "\r")]
    [DataRow("Ctrl+a", "\u0001")]
    [DataRow("Ctrl+Alt+a", "\x1b\u0001")]
    [DataRow("Ctrl+Alt", "")]
    [DataRow("Ctrl+[", "\x1b")]
    [DataRow("Ctrl+@", "\0")]
    [DataRow("Alt+word", "\u001bw\u001bo\u001br\u001bd")]
    [DataRow("Shift+abc", "ABC")]
    public async Task Play_KeysAndModifiers_ProduceExpectedTerminalInput(string source, string expected)
    {
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        using var result = await new TapePlayer().PlayAsync(
            new TapeParser().Parse("Set TypingSpeed 0 " + source), terminal);

        Assert.AreEqual(expected, workload.Input.ToString());
        Assert.AreEqual(2, result.CompletedCommandCount);
        if (source == "Enter 0001")
            Assert.IsEmpty(result.Diagnostics);
    }

    [TestMethod]
    public async Task Validate_LateTimingSettings_PreservesVhsOrderingForPlaybackContext()
    {
        TapePlayContext? observed = null;
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Observe"] = _ => context =>
        {
            observed = context;
            return TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
        };
        var parser = new TapeParser(options);
        var tape = parser.Parse("Set TypingSpeed 10ms Set WaitTimeout 2s Type 'x' Set TypingSpeed 20ms Set WaitTimeout 3s Observe");
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        var validation = await new TapePlayer().ValidateAsync(tape, terminal);

        Assert.IsTrue(validation.CanExecute);
        Assert.IsNotNull(observed);
        Assert.AreEqual(TimeSpan.FromMilliseconds(20), observed.TypingSpeed);
        Assert.AreEqual(TimeSpan.FromSeconds(2), observed.WaitTimeout);
        Assert.IsTrue(validation.Diagnostics.Any(d => d.Severity == TapeDiagnosticSeverity.Warning));
        Assert.AreEqual("", workload.Input.ToString());
    }

    [TestMethod]
    public async Task Play_ReplacedOutputSyntax_DoesNotCreateBuiltinCapture()
    {
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Output"] = parse =>
        {
            Assert.AreEqual("unsupported.mp4", parse.Reader.Read().Value);
            return context => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder().WithOptions(context.SequenceOptions));
        };

        using var result = await new TapePlayer().PlayAsync(new TapeParser(options).Parse("Output 'unsupported.mp4'"), terminal);

        Assert.AreEqual(1, result.CompletedCommandCount);
        Assert.IsEmpty(result.Artifacts);
    }

    [TestMethod]
    public async Task Play_LateCallbackErrors_CollectsErrorsBeforeAnyInputResizeOrCapture()
    {
        using var files = new CaptureDirectory();
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var originalResizeCount = workload.ResizeCount;
        var prepared = new List<string>();
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Fail"] = parse =>
        {
            var message = parse.Reader.Read().Value;
            return _ =>
            {
                prepared.Add(message);
                return TapeCommandResult.Reject(message);
            };
        };
        var tape = new TapeParser(options).Parse(
            "Set TypingSpeed 0 Type 'never sent'\nFail 'first error'\nFail 'second error'", "late.tape");

        var error = await Assert.ThrowsAsync<TapeValidationException>(() => new TapePlayer().PlayAsync(tape, terminal,
            new TapePlaybackOptions
            {
                TerminalSize = new Size(30, 6),
                Capture = new TapeCaptureOptions
                {
                    AsciinemaPath = files.File("blocked.cast"),
                    GoldenTextPath = files.File("blocked.txt")
                }
            }));

        TestSeq.AreEqual(["first error", "second error"], prepared);
        var diagnostics = error.Diagnostics.Where(d => d.Code == "TAPE_COMMAND").ToArray();
        TestSeq.AreEqual(["first error", "second error"], diagnostics.Select(d => d.Message));
        TestSeq.AreEqual(tape.Commands.Skip(2).Select(c => c.Span), diagnostics.Select(d => d.Span));
        Assert.IsTrue(diagnostics.All(d => d.Stage == TapeDiagnosticStage.Compilation));
        Assert.AreEqual("", workload.Input.ToString());
        Assert.AreEqual(originalResizeCount, workload.ResizeCount);
        Assert.IsFalse(System.IO.File.Exists(files.File("blocked.cast")));
        Assert.IsFalse(System.IO.File.Exists(files.File("blocked.txt")));
    }

    [TestMethod]
    [DataRow("Type")]
    [DataRow("Source")]
    public async Task Play_ReplacedBuiltinSyntax_ExecutesOnlyReplacement(string keyword)
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions[keyword] = parse =>
        {
            parse.Reader.Read();
            return context => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder()
                .WithOptions(context.SequenceOptions).Type("replacement"));
        };
        var tape = new TapeParser(options).Parse($"{keyword} 'missing.tape'");
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        using var result = await new TapePlayer().PlayAsync(tape, terminal);

        Assert.AreEqual("replacement", workload.Input.ToString());
        Assert.AreEqual(1, result.CompletedCommandCount);
        TestSeq.IsType<TapeExtensionCommand>(TestSeq.Single(tape.Commands));
    }

    [TestMethod]
    [DataRow("Type", "Type 'blocked'")]
    [DataRow("Source", "Source 'missing.tape'")]
    public async Task Validate_RemovedBuiltinInSource_ReportsIncludedParsingError(string keyword, string includedText)
    {
        using var files = new CaptureDirectory();
        await System.IO.File.WriteAllTextAsync(files.File("include.tape"), includedText);
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Remove(keyword);
        var parser = new TapeParser(options);
        var tape = parser.Parse(keyword == "Source" ? "" : "Source 'include.tape'");
        if (keyword == "Source")
            tape.Commands.Add(new TapeSourceCommand("include.tape", default));
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        var validation = await new TapePlayer().ValidateAsync(tape, terminal, new TapePlaybackOptions { WorkingDirectory = files.Path });

        Assert.IsFalse(validation.CanExecute);
        Assert.IsTrue(validation.Diagnostics.Any(d => d.Stage == TapeDiagnosticStage.Parsing &&
            d.Span.SourceName == files.File("include.tape")));
        Assert.AreEqual("", workload.Input.ToString());
    }

    [TestMethod]
    public async Task Play_ParsedBuiltinRecordsEditedWithExpressions_UsesCurrentFieldValues()
    {
        var parser = new TapeParser();
        var tape = parser.Parse("Set TypingSpeed 1s Type 'original' Enter 1");
        var zeroSpeed = TestSeq.IsType<TapeSetCommand>(parser.Parse("Set TypingSpeed 0").Commands[0]).Value;
        tape.Commands[0] = TestSeq.IsType<TapeSetCommand>(tape.Commands[0]) with { Value = zeroSpeed };
        tape.Commands[1] = TestSeq.IsType<TapeTypeCommand>(tape.Commands[1]) with { Text = "edited" };
        tape.Commands[2] = TestSeq.IsType<TapeKeyCommand>(tape.Commands[2]) with { Key = "Tab" };
        var workload = new RecordingWorkload();
        var clock = new FakeTimeProvider();
        await using var terminal = CreateTerminal(workload, clock);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var result = await new TapePlayer().PlayAsync(tape, terminal, cancellationToken: cancellation.Token);

        Assert.AreEqual("edited\t", workload.Input.ToString());
        Assert.AreEqual(3, result.CompletedCommandCount);
    }

    [TestMethod]
    public async Task Play_ProgrammaticBuiltinCommand_ExecutesWithoutParserRegistration()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions.Clear();
        var tape = new TapeParser(options).Parse("");
        tape.Commands.Add(new TapeTypeCommand("programmatic", null, default));
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        using var result = await new TapePlayer().PlayAsync(tape, terminal);

        Assert.AreEqual("programmatic", workload.Input.ToString());
        Assert.AreEqual(1, result.CompletedCommandCount);
    }

    [TestMethod]
    public async Task Play_BuiltinDelegateAliases_ProduceAndExecuteTypedCommands()
    {
        var options = new TapeParserOptions();
        options.SyntaxExtensions["Pause"] = options.SyntaxExtensions["Sleep"];
        options.SyntaxExtensions["Write"] = options.SyntaxExtensions["Type"];
        options.SyntaxExtensions.Remove("Sleep");
        var parser = new TapeParser(options);
        var tape = parser.Parse("Set TypingSpeed 0 Pause 0 Write 'original'");
        var sleep = TestSeq.IsType<TapeSleepCommand>(tape.Commands[1]);
        var type = TestSeq.IsType<TapeTypeCommand>(tape.Commands[2]);
        Assert.IsNotNull(sleep.PrepareCallback);
        Assert.IsNotNull(type.PrepareCallback);
        tape.Commands[2] = type with { Text = "aliased" };
        Assert.IsFalse(parser.TryParse("Sleep 0", out _));
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);

        using var result = await new TapePlayer().PlayAsync(tape, terminal);

        Assert.AreEqual("aliased", workload.Input.ToString());
        Assert.AreEqual(3, result.CompletedCommandCount);
    }

    [TestMethod]
    [DataRow("Source", 1)]
    [DataRow("Output", 1)]
    [DataRow("Type", 2)]
    [DataRow("Sleep", 1)]
    public async Task Play_WrappedBuiltinReturnsError_RejectsEvenSkippedIncludeDirectives(string keyword, int expectedErrors)
    {
        using var files = new CaptureDirectory();
        var includedPath = files.File("include.tape");
        await System.IO.File.WriteAllTextAsync(includedPath, "Output 'ignored.mp4' Type 'included'");
        var options = new TapeParserOptions();
        var builtin = options.SyntaxExtensions[keyword];
        var calls = 0;
        options.SyntaxExtensions[keyword] = parse =>
        {
            var original = builtin(parse);
            return play =>
            {
                calls++;
                var result = original(play);
                Assert.IsNotNull(result.SequenceBuilder);
                Assert.IsNull(result.ErrorMessage);
                return TapeCommandResult.Reject($"Wrapped {keyword} rejected.");
            };
        };
        var tape = new TapeParser(options).Parse("Type 'earlier' Source 'include.tape' Sleep 0", "wrapped.tape");
        Assert.IsTrue(tape.Commands.All(command => command is not TapeExtensionCommand));
        var workload = new RecordingWorkload();
        await using var terminal = CreateTerminal(workload);
        var resizeCount = workload.ResizeCount;
        var playback = new TapePlaybackOptions
        {
            WorkingDirectory = files.Path,
            TerminalSize = new Size(30, 6),
            Capture = new TapeCaptureOptions
            {
                AsciinemaPath = files.File("blocked.cast"),
                GoldenTextPath = files.File("blocked.txt")
            }
        };
        var player = new TapePlayer();

        var validation = await player.ValidateAsync(tape, terminal, playback);
        Assert.IsFalse(validation.CanExecute);
        Assert.AreEqual(expectedErrors, calls);
        calls = 0;
        var error = await Assert.ThrowsExactlyAsync<TapeValidationException>(() => player.PlayAsync(tape, terminal, playback));

        Assert.AreEqual(expectedErrors, calls);
        Assert.AreEqual(expectedErrors, error.Diagnostics.Count);
        Assert.IsTrue(error.Diagnostics.All(d => d.Code == "TAPE_COMMAND" &&
            d.Stage == TapeDiagnosticStage.Compilation && d.Message == $"Wrapped {keyword} rejected."));
        TestSeq.AreEqual(validation.Diagnostics, error.Diagnostics);
        if (keyword == "Output")
        {
            var diagnostic = TestSeq.Single(error.Diagnostics);
            Assert.AreEqual(includedPath, diagnostic.Span.SourceName);
            Assert.AreEqual(0, diagnostic.Span.Offset);
            Assert.AreEqual("Output 'ignored.mp4'".Length, diagnostic.Span.Length);
        }
        else if (keyword == "Source")
        {
            Assert.AreEqual(tape.Commands[1].Span, TestSeq.Single(error.Diagnostics).Span);
        }
        Assert.AreEqual("", workload.Input.ToString());
        Assert.AreEqual(resizeCount, workload.ResizeCount);
        Assert.IsFalse(System.IO.File.Exists(files.File("blocked.cast")));
        Assert.IsFalse(System.IO.File.Exists(files.File("blocked.txt")));
    }

    [TestMethod]
    public async Task Validate_UnconfirmedRemoteResize_RejectsBeforeCaptureOrTransport()
    {
        using var files = new CaptureDirectory();
        var workload = new Hmp1WorkloadAdapter(new Hmp1ClientOptions
        {
            StreamFactory = _ => throw new AssertFailedException("Preflight must not connect a workload.")
        });
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 4).Build();
        var tape = new TapeParser().Parse("");
        var options = new TapePlaybackOptions
        {
            TerminalSize = new Size(30, 6),
            Capture = new TapeCaptureOptions { AsciinemaPath = files.File("remote.cast") }
        };

        var validation = await new TapePlayer().ValidateAsync(tape, terminal, options);
        await Assert.ThrowsAsync<TapeValidationException>(() => new TapePlayer().PlayAsync(tape, terminal, options));

        Assert.IsFalse(validation.CanExecute);
        Assert.IsTrue(validation.Diagnostics.Any(d => d.Code == "TAPE_RESIZE_UNSUPPORTED"));
        Assert.IsFalse(System.IO.File.Exists(files.File("remote.cast")));
    }

    private static Hex1bTerminal CreateTerminal(RecordingWorkload workload, TimeProvider? clock = null)
    {
        var builder = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().WithDimensions(20, 4);
        if (clock is not null)
            builder.WithTimeProvider(clock);
        return builder.Build();
    }

    private sealed record CustomCommand(TapeSourceSpan Span) : TapeCommand(Span);

    private sealed class CaptureDirectory : IDisposable
    {
        internal string Path { get; } = Directory.CreateDirectory(System.IO.Path.Combine("TestResults", $"hex1b-tape-{Guid.NewGuid():N}")).FullName;
        internal string File(string name) => System.IO.Path.Combine(Path, name);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class RecordingWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        internal StringBuilder Input { get; } = new();
        internal int ResizeCount { get; private set; }
        internal bool Disposed { get; private set; }
        public event Action? Disconnected { add { } remove { } }
        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
        {
            while (await _output.Reader.WaitToReadAsync(ct))
            {
                if (_output.Reader.TryRead(out var data))
                    return data;
            }
            return ReadOnlyMemory<byte>.Empty;
        }
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Input.Append(Encoding.UTF8.GetString(data.Span));
            return _output.Writer.WriteAsync(data.ToArray(), ct);
        }
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
        {
            ResizeCount++;
            return ValueTask.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            _output.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
