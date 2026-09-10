using Hex1b.Automation;

namespace Hex1b.Tests;

[TestClass]
public class TapePlayerOwnedTerminalTests
{
    [TestMethod]
    [TestCategory("Unix")]
    public async Task Play_ShellAndEnvironmentDeclarations_OverrideOptionsWithoutChangingHost()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("This test exercises the Unix bash launch profile.");
        var original = Environment.GetEnvironmentVariable("HEX1B_TAPE_TEST_VALUE");
        var tape = new TapeParser().Parse("""
            Set TypingSpeed 0
            Set WaitTimeout 10s
            Env HEX1B_TAPE_TEST_VALUE "tape"
            Set Shell "bash"
            Wait />$/
            Type `printf 'RUN:%s:END\n' "$HEX1B_TAPE_TEST_VALUE"`
            Enter
            Wait+Screen /\nRUN:tape:END\n/
            """);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        using var result = await new TapePlayer().PlayAsync(tape, options: new TapePlaybackOptions
        {
            TerminalFactory = builder => builder.WithDimensions(100, 30).Build(),
            DefaultShell = "unsupported-fallback",
            Environment = new Dictionary<string, string> { ["HEX1B_TAPE_TEST_VALUE"] = "base" }
        }, cancellationToken: cancellation.Token);

        Assert.IsTrue(result.FinalSnapshot.ContainsText("RUN:tape:END"));
        Assert.AreEqual(original, Environment.GetEnvironmentVariable("HEX1B_TAPE_TEST_VALUE"));
        Assert.IsNull(result.ProcessExitCode);
    }

    [TestMethod]
    [TestCategory("Unix")]
    public async Task Play_EmptyTape_CleansUpInteractiveShellWithoutWaitingForExit()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("This test exercises the Unix bash launch profile.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        using var result = await new TapePlayer().PlayAsync(new TapeParser().Parse(""),
            options: new TapePlaybackOptions { DefaultShell = "bash" }, cancellationToken: cancellation.Token);

        Assert.AreEqual(0, result.CompletedCommandCount);
        Assert.AreEqual(80, result.TerminalSize.Width);
        Assert.AreEqual(24, result.TerminalSize.Height);
        Assert.IsNull(result.ProcessExitCode);
    }

    [TestMethod]
    public async Task Validate_Require_UsesEffectiveChildPathRatherThanHostPath()
    {
        var directory = Directory.CreateTempSubdirectory("hex1b-tape-path-");
        try
        {
            var result = await new TapePlayer().ValidateAsync(new TapeParser().Parse("Require 'dotnet'"),
                options: new TapePlaybackOptions
                {
                    InheritEnvironment = false,
                    Environment = new Dictionary<string, string> { ["PATH"] = directory.FullName }
                });

            Assert.IsFalse(result.CanExecute);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Message.Contains("'dotnet'", StringComparison.Ordinal)));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Play_InvalidPreflight_DoesNotCreateCapture()
    {
        var directory = Directory.CreateTempSubdirectory("hex1b-tape-run-");
        try
        {
            var output = Path.Combine(directory.FullName, "blocked.cast");
            await Assert.ThrowsAsync<TapeValidationException>(() => new TapePlayer().PlayAsync(
                new TapeParser().Parse("Type 'not executed' Screenshot 'unsupported.png'"),
                options: new TapePlaybackOptions
                {
                    Capture = new TapeCaptureOptions { AsciinemaPath = output }
                }));
            Assert.IsFalse(File.Exists(output));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Unix")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Play_NaturalExit_ReportsCodeAndRejectsSubsequentInput(bool sendMoreInput)
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("This test exercises the Unix bash launch profile.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var source = """
            Set TypingSpeed 0
            Wait@10s />$/
            Type "exit 7"
            Enter
            Sleep 1s
            """;
        if (sendMoreInput)
            source += "\nType 'too late'";
        var tape = new TapeParser().Parse(source);
        var player = new TapePlayer();
        var options = new TapePlaybackOptions { DefaultShell = "bash" };

        if (sendMoreInput)
        {
            var error = await Assert.ThrowsAsync<TapePlaybackException>(
                () => player.PlayAsync(tape, options: options, cancellationToken: cancellation.Token));
            Assert.AreEqual(5, error.CompletedCommandCount);
            Assert.IsInstanceOfType<TapeTypeCommand>(error.Command);
            Assert.IsInstanceOfType<IOException>(error.InnerException);
        }
        else
        {
            using var result = await player.PlayAsync(tape, options: options, cancellationToken: cancellation.Token);
            Assert.AreEqual(7, result.ProcessExitCode);
        }
    }

    [TestMethod]
    [TestCategory("Unix")]
    public async Task Play_Cancellation_StopsOwnedInteractiveShell()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("This test exercises the Unix bash launch profile.");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        var tape = new TapeParser().Parse("Sleep 60m");

        await Assert.ThrowsAsync<OperationCanceledException>(() => new TapePlayer()
            .PlayAsync(tape, options: new TapePlaybackOptions { DefaultShell = "bash" }, cancellationToken: cancellation.Token)
            .WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public async Task Validate_LateRequire_IsIgnoredWithDiagnostic()
    {
        var tape = new TapeParser().Parse("Sleep 0 Require 'hex1b-nonexistent-late-require-8675309'");

        var result = await new TapePlayer().ValidateAsync(tape);

        Assert.IsTrue(result.CanExecute);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Severity == TapeDiagnosticSeverity.Warning &&
            d.Message.StartsWith("Late Require", StringComparison.Ordinal)));
    }
}
