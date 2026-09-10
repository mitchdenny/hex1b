using System.Text.Json;
using Hex1b.Automation;
using Hex1b.Tokens;
using Microsoft.Extensions.Time.Testing;

namespace Hex1b.Tests;

[TestClass]
public class TapePlayerCaptureTests
{
    [TestMethod]
    public async Task Play_HiddenIntervals_ResynchronizesStateAndCompressesOnlyRecordingTime()
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-capture-{Guid.NewGuid():N}"));
        try
        {
            var clock = new FakeTimeProvider();
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(20, 4)
                .WithTimeProvider(clock).Build();
            var options = new TapeParserOptions();
            options.SyntaxExtensions["Advance"] = parse =>
            {
                var duration = TimeSpan.FromSeconds(double.Parse(parse.Reader.Read().Value,
                    System.Globalization.CultureInfo.InvariantCulture));
                var output = parse.Reader.Read().Value;
                return context => TapeCommandResult.Accept(new Hex1bTerminalInputSequenceBuilder()
                    .WithOptions(context.SequenceOptions)
                    .WaitUntil(snapshot =>
                    {
                        clock.Advance(duration);
                        snapshot.Terminal.ApplyTokens(AnsiTokenizer.Tokenize(output));
                        return true;
                    }, TimeSpan.FromMinutes(1)));
            };
            var tape = new TapeParser(options).Parse(
                "Advance 1 'before' Hide Advance 10 'HIDDEN_INTERMEDIATE' " +
                "Advance 0 '\x1b[2J\x1b[Hready' Show Advance 1 'after'");
            var castPath = Path.Combine(directory.FullName, "capture.cast");
            var goldenPath = Path.Combine(directory.FullName, "capture.txt");

            using var result = await new TapePlayer().PlayAsync(tape, terminal, new TapePlaybackOptions
            {
                Capture = new TapeCaptureOptions { AsciinemaPath = castPath, GoldenTextPath = goldenPath }
            });

            Assert.AreEqual(TimeSpan.FromSeconds(12), result.Elapsed);
            var lines = await File.ReadAllLinesAsync(castPath);
            using var header = JsonDocument.Parse(lines[0]);
            Assert.AreEqual(2, header.RootElement.GetProperty("version").GetInt32());
            var previous = 0d;
            foreach (var line in lines.Skip(1))
            {
                using var item = JsonDocument.Parse(line);
                var timestamp = item.RootElement[0].GetDouble();
                Assert.IsTrue(timestamp >= previous, "Recording timestamps must not go backwards.");
                Assert.IsTrue(timestamp <= 2.001, "Hidden time must be removed from recording timestamps.");
                previous = timestamp;
                Assert.IsFalse(item.RootElement[2].GetString()!.Contains("HIDDEN_INTERMEDIATE", StringComparison.Ordinal));
            }
            Assert.AreEqual(2d, previous, 0.001);
            var golden = await File.ReadAllTextAsync(goldenPath);
            Assert.AreEqual(3, golden.Split(new string('\u2500', 80), StringSplitOptions.None).Length - 1);
            Assert.IsFalse(golden.Contains("HIDDEN_INTERMEDIATE", StringComparison.Ordinal));
            Assert.IsTrue(result.FinalSnapshot.ContainsText("readyafter"));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Play_TextOutputDirective_StartsGoldenAtDirectiveRatherThanEarlierCommands()
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-golden-{Guid.NewGuid():N}"));
        try
        {
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(20, 4).Build();
            var tape = new TapeParser().Parse("Set TypingSpeed 0 Output 'snap.txt' Sleep 0");

            using var result = await new TapePlayer().PlayAsync(tape, terminal,
                new TapePlaybackOptions { WorkingDirectory = directory.FullName });

            var golden = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "snap.txt"));
            Assert.AreEqual(2, golden.Split(new string('\u2500', 80), StringSplitOptions.None).Length - 1);
            Assert.IsFalse(golden.Contains('\r'));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Play_WaitTimeout_PreservesSourceAndOriginalFailure()
    {
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(20, 4).Build();
        var tape = new TapeParser().Parse("Wait@20ms /never matches/", "timeout.tape");

        var error = await Assert.ThrowsAsync<TapePlaybackException>(() => new TapePlayer().PlayAsync(tape, terminal));

        Assert.AreEqual(0, error.CompletedCommandCount);
        Assert.AreEqual("timeout.tape", error.Command!.Span.SourceName);
        Assert.IsInstanceOfType<WaitUntilTimeoutException>(error.InnerException);
    }

    [TestMethod]
    public async Task Play_Scrollback_GoldenAndScreenWaitReadFromActiveBufferStart()
    {
        var directory = Directory.CreateDirectory(Path.Combine("TestResults", $"hex1b-tape-buffer-{Guid.NewGuid():N}"));
        try
        {
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().WithDimensions(12, 2)
                .WithScrollback(20).Build();
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("old-first\r\nold-second\r\nrecent-third\r\nrecent-last"));
            using (var current = terminal.CreateSnapshot())
                Assert.IsFalse(current.ContainsText("old-first"));
            var goldenPath = Path.Combine(directory.FullName, "buffer.txt");

            using var result = await new TapePlayer().PlayAsync(
                new TapeParser().Parse("Wait+Screen@100ms /old-first/"), terminal,
                new TapePlaybackOptions { Capture = new TapeCaptureOptions { GoldenTextPath = goldenPath } });

            var golden = await File.ReadAllTextAsync(goldenPath);
            Assert.IsTrue(golden.StartsWith("old-first\nold-second\n", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

}
