using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class TerminalTitleTests
{
    [TestMethod]
    [DataRow("0", "\x1b]", "\x07")]
    [DataRow("1", "\x1b]", "\x07")]
    [DataRow("2", "\x1b]", "\x07")]
    [DataRow("0", "\x1b]", "\x1b\\")]
    [DataRow("1", "\x1b]", "\x1b\\")]
    [DataRow("2", "\x1b]", "\x1b\\")]
    [DataRow("2", "\u009d", "\u009c")]
    public async Task OscTitle_SemicolonsAndUnicode_RoundTripsWithoutChangingOtherState(
        string command, string introducer, string terminator)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens([new OscToken("0", "", "original")]);
        const string title = ";repo;main;;東京 😀;<b>literal</b>;";
        var token = TestSeq.IsType<OscToken>(TestSeq.Single(
            AnsiTokenizer.Tokenize($"{introducer}{command};{title}{terminator}")));
        Assert.AreEqual("", token.Parameters);
        Assert.AreEqual(title, token.Payload);
        var roundTrip = TestSeq.IsType<OscToken>(TestSeq.Single(
            AnsiTokenizer.Tokenize(AnsiTokenSerializer.Serialize(token))));
        Assert.AreEqual(command, roundTrip.Command);
        Assert.AreEqual(title, roundTrip.Payload);

        workload.Write($"{introducer}{command};{title}{terminator}done");
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("done"), TimeSpan.FromSeconds(5), "complete OSC applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);

        Assert.AreEqual(command == "1" ? "original" : title, terminal.WindowTitle);
        Assert.AreEqual(command == "2" ? "original" : title, terminal.IconName);
        Assert.AreEqual(terminal.WindowTitle, snapshot.WindowTitle);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("1")]
    [DataRow("2")]
    public async Task ApplyTokens_ControlsAndMalformedSurrogates_NormalizesBeforeStorageAndEvents(string command)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var controls = new string(Enumerable.Range(0, 32).Concat(Enumerable.Range(127, 33))
            .Select(value => (char)value).ToArray());
        const string text = "東京 😀<b>text</b>\u202e";
        var titleEvents = new List<string>();
        var iconEvents = new List<string>();
        terminal.WindowTitleChanged += title =>
        {
            Assert.AreEqual(title, terminal.WindowTitle);
            titleEvents.Add(title);
        };
        terminal.IconNameChanged += icon => iconEvents.Add(icon);

        // Pre-tokenized strings preserve malformed UTF-16 for the authoritative setter.
        terminal.ApplyTokens([new OscToken(command, "", controls + text + "\ud800x\udc00")]);

        var expected = text + "\ufffdx\ufffd";
        Assert.AreEqual(command == "1" ? "" : expected, terminal.WindowTitle);
        Assert.AreEqual(command == "2" ? "" : expected, terminal.IconName);
        TestSeq.AreEqual(command == "1" ? [] : new[] { expected }, titleEvents);
        TestSeq.AreEqual(command == "2" ? [] : new[] { expected }, iconEvents);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(terminal.WindowTitle, snapshot.WindowTitle);
    }

    [TestMethod]
    [DataRow(4094)]
    [DataRow(4095)]
    [DataRow(4096)]
    public async Task ApplyTokens_LengthBoundary_TruncatesAtUnicodeScalarWithoutCountingControls(int prefixLength)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var prefix = new string('a', prefixLength);
        terminal.ApplyTokens([new OscToken("0", "", "\0\u0080" + prefix + "😀tail")]);

        var expected = prefix + (prefixLength == 4094 ? "😀" : "");
        Assert.AreEqual(expected, terminal.WindowTitle);
        Assert.AreEqual(expected, terminal.IconName);
        Assert.IsTrue(terminal.WindowTitle.Length <= 4096);
        Assert.IsFalse(char.IsHighSurrogate(terminal.WindowTitle[^1]));
    }

    [TestMethod]
    public async Task ApplyTokens_EquivalentNormalizedTitlesAndEmptyClear_OnlyNotifiesChanges()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var titleEvents = new List<string>();
        var iconEvents = new List<string>();
        terminal.WindowTitleChanged += titleEvents.Add;
        terminal.IconNameChanged += iconEvents.Add;
        var bounded = new string('a', 4096);
        terminal.ApplyTokens([
            new OscToken("0", "", bounded + "first"),
            new OscToken("0", "", "\0" + bounded + "second"),
            new OscToken("1", "", "icon"),
            new OscToken("2", "", ""),
            new OscToken("2", "", "\u007f\u009f"),
        ]);

        TestSeq.AreEqual(new[] { bounded, "" }, titleEvents);
        TestSeq.AreEqual(new[] { bounded, "icon" }, iconEvents);
        Assert.AreEqual("", terminal.WindowTitle);
        Assert.AreEqual("icon", terminal.IconName);
        terminal.ApplyTokens([new OscToken("0", "", "")]);
        TestSeq.AreEqual(new[] { bounded, "icon", "" }, iconEvents);
        TestSeq.AreEqual(new[] { bounded, "" }, titleEvents);
    }

    [TestMethod]
    public async Task CreateSnapshot_LaterTitleChanges_DoNotMutateCapturedTitle()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        using var initial = terminal.CreateSnapshot();
        terminal.ApplyTokens([new OscToken("2", "", "captured;title")]);
        using var captured = terminal.CreateSnapshot();
        terminal.ApplyTokens([new OscToken("2", "", "later"), new OscToken("2", "", "")]);
        using var cleared = terminal.CreateSnapshot();

        Assert.AreEqual("", initial.WindowTitle);
        Assert.AreEqual("captured;title", captured.WindowTitle);
        Assert.AreEqual("", cleared.WindowTitle);
    }

    [TestMethod]
    [DataRow("\x1b" + "c")]
    [DataRow("\x1b[!p")]
    [DataRow("\x1b[2J")]
    [DataRow("\x1b[?1049h\x1b[?1049l")]
    public async Task RawOutput_ResetOrScreenChange_PreservesCurrentAndSavedTitle(string reset)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]2;saved;title\x07\x1b]1;saved;icon\x07\x1b]22;\x07" +
            "\x1b]0;current\x07"));

        workload.Write(reset + "reset-done");
        using var resetSnapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("reset-done"), TimeSpan.FromSeconds(5), "raw reset applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        Assert.AreEqual("current", resetSnapshot.WindowTitle);
        Assert.AreEqual("current", terminal.IconName);

        terminal.ApplyTokens([new OscToken("23", "", "")]);
        Assert.AreEqual("saved;title", terminal.WindowTitle);
        Assert.AreEqual("saved;icon", terminal.IconName);
        terminal.ApplyTokens([new OscToken("23", "", "")]);
        Assert.AreEqual("saved;title", terminal.WindowTitle);
    }

    [TestMethod]
    public async Task TerminalWidgetHandle_TitleNormalizationAndStack_MatchesTerminal()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        await using var handle = new TerminalWidgetHandle(20, 5);
        var title = "\0" + new string('a', 4093) + "\ud800😀tail";
        OscToken[] tokens =
        [
            new("0", "", title),
            new("22", "", ""),
            new("0", "", "changed\u0085"),
            new("23", "", ""),
        ];
        foreach (var token in tokens)
        {
            terminal.ApplyTokens([token]);
            await handle.WriteOutputWithImpactsAsync(
                [AppliedToken.WithNoCellImpacts(token, 0, 0, 0, 0)],
                TestContext.Current.CancellationToken);
            Assert.AreEqual(terminal.WindowTitle, handle.WindowTitle);
            Assert.AreEqual(terminal.IconName, handle.IconName);
        }
        Assert.AreEqual(new string('a', 4093) + "\ufffd😀", handle.WindowTitle);
    }
}
