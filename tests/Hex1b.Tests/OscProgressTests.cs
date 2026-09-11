using System.Text;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class OscProgressTests
{
    [TestMethod]
    [DataRow("0", TerminalProgressState.None, null)]
    [DataRow("0;", TerminalProgressState.None, null)]
    [DataRow("0;ignored", TerminalProgressState.None, null)]
    [DataRow("1;0", TerminalProgressState.Normal, 0)]
    [DataRow("1;100", TerminalProgressState.Normal, 100)]
    [DataRow("1;007", TerminalProgressState.Normal, 7)]
    [DataRow("2;25", TerminalProgressState.Error, 25)]
    [DataRow("3", TerminalProgressState.Indeterminate, null)]
    [DataRow("3;", TerminalProgressState.Indeterminate, null)]
    [DataRow("3;-99999999999999", TerminalProgressState.Indeterminate, null)]
    [DataRow("4;80", TerminalProgressState.Warning, 80)]
    public async Task Osc9_ValidProgress_UpdatesWithoutCellOrCursorEffects(
        string payload, TerminalProgressState state, int? percentage)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("content"));
        var token = TestSeq.IsType<OscToken>(TestSeq.Single(
            AnsiTokenizer.Tokenize($"\x1b]9;4;{payload}\x07")));
        Assert.AreEqual("4", token.Parameters);
        Assert.AreEqual(payload, token.Payload);
        var impacts = TestSeq.Single(terminal.ApplyTokensWithImpacts([token]));

        Assert.AreEqual(state, terminal.Progress.State);
        Assert.AreEqual(percentage, terminal.Progress.Percentage);
        Assert.AreEqual(0, impacts.CellImpacts.Count);
        Assert.AreEqual(impacts.CursorXBefore, impacts.CursorXAfter);
        Assert.AreEqual(impacts.CursorYBefore, impacts.CursorYAfter);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(terminal.Progress, snapshot.Progress);
        Assert.IsTrue(snapshot.ContainsText("content"));
        Assert.AreEqual(TerminalShellIntegrationPhase.Unknown, snapshot.ShellIntegration.Phase);
    }

    [TestMethod]
    [DataRow("9")]
    [DataRow("9;4")]
    [DataRow("9;4;")]
    [DataRow("9;5;1;50")]
    [DataRow("9;4;5;50")]
    [DataRow("9;4;01;50")]
    [DataRow("9;4;+1;50")]
    [DataRow("9;4;1")]
    [DataRow("9;4;1;")]
    [DataRow("9;4;1;-1")]
    [DataRow("9;4;1;+1")]
    [DataRow("9;4;1;101")]
    [DataRow("9;4;1;2147483648")]
    [DataRow("9;4;1;999999999999999999999999")]
    [DataRow("9;4;1; 50")]
    [DataRow("9;4;1;50 ")]
    [DataRow("9;4;1;1.0")]
    [DataRow("9;4;1;５０")]
    [DataRow("9;4;1;50;")]
    [DataRow("9;4;0;ignored;extra")]
    [DataRow("9;4;3;;")]
    public async Task Osc9_InvalidProgress_IgnoresUntrustedMutation(string body)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens([new OscToken("9", "4", "2;40")]);
        var before = terminal.Progress;
        var changes = 0;
        terminal.ProgressChanged += _ => changes++;

        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b]{body}\x07"));

        Assert.AreEqual(before, terminal.Progress);
        Assert.AreEqual(0, changes);
    }

    [TestMethod]
    public async Task ProgressChanged_DefaultsAndRepeatedValues_OnlyNotifiesDistinctChanges()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        var changes = new List<TerminalProgress>();
        terminal.ProgressChanged += progress =>
        {
            Assert.AreEqual(progress, terminal.Progress);
            changes.Add(progress);
        };
        Assert.AreEqual(0, changes.Count);
        Assert.AreEqual(TerminalProgressState.None, terminal.Progress.State);
        Assert.IsNull(terminal.Progress.Percentage);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]9;4;0\x07\x1b]9;4;1;10\x07\x1b]9;4;1;010\x07" +
            "\x1b]9;4;3\x07\x1b]9;4;3;ignored\x07\x1b]9;4;0\x07"));

        TestSeq.AreEqual(new[]
        {
            new TerminalProgress(TerminalProgressState.Normal, 10),
            new TerminalProgress(TerminalProgressState.Indeterminate, null),
            TerminalProgress.Default
        }, changes);
    }

    [TestMethod]
    [DataRow("\x1b]", "\x07")]
    [DataRow("\x1b]", "\x1b\\")]
    [DataRow("\u009d", "\u009c")]
    public async Task RawOutput_EveryByteSplit_AppliesOnlyCompleteProgress(string introducer, string terminator)
    {
        var bytes = Encoding.UTF8.GetBytes($"{introducer}9;4;4;73{terminator}");
        for (var split = 1; split < bytes.Length; split++)
        {
            using var workload = new Hex1bAppWorkloadAdapter();
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
            var changed = new TaskCompletionSource<TerminalProgress>(TaskCreationOptions.RunContinuationsAsynchronously);
            terminal.ProgressChanged += state => changed.TrySetResult(state);

            workload.Write(bytes.AsMemory(0, split));
            workload.Write(bytes.AsMemory(split));
            var progress = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.AreEqual(TerminalProgressState.Warning, progress.State);
            Assert.AreEqual(73, progress.Percentage);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual(0, snapshot.CursorX);
            Assert.AreEqual(0, snapshot.CursorY);
            Assert.AreEqual("", snapshot.GetText().Trim());
        }
    }

    [TestMethod]
    [DataRow("\x1b[!p")]
    [DataRow("\x1b[2J")]
    [DataRow("\x1b[?1049h\x1b[?1049l")]
    public async Task ScreenChange_PreservesActivity_AndRisClearsBoth(string sequence)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;2;25\x07\x1b]133;D;-1\x07"));
        using var before = terminal.CreateSnapshot();

        terminal.ApplyTokens(AnsiTokenizer.Tokenize(sequence));
        terminal.Resize(30, 8);
        Assert.AreEqual(before.Progress, terminal.Progress);
        Assert.AreEqual(before.ShellIntegration, terminal.ShellIntegration);

        terminal.ApplyTokens([RisToken.Instance]);
        Assert.AreEqual(TerminalProgress.Default, terminal.Progress);
        Assert.AreEqual(TerminalShellIntegration.Default, terminal.ShellIntegration);
        Assert.AreEqual(TerminalProgressState.Error, before.Progress.State);
        Assert.AreEqual(-1, before.ShellIntegration.LastExitCode);
    }

    [TestMethod]
    public async Task CreateSnapshot_ScrollbackAndHistoricalText_CapturesImmutableCurrentActivity()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(20, 3).WithScrollback().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "old\r\nmiddle\r\nnew\r\nlast\x1b]9;4;4;90\x07\x1b]133;D;7\x07"));
        using var captured = terminal.CreateSnapshot(10);
        using var historical = new Hex1bTerminalSnapshot(terminal,
            terminal.CaptureSnapshotState(0, ScrollbackWidth.CurrentTerminal, textViewportTop: 0),
            ScrollbackWidth.CurrentTerminal, TerminalCell.Empty);
        Assert.IsTrue(historical.ContainsText("old"));
        Assert.AreEqual(captured.Progress, historical.Progress);
        Assert.AreEqual(captured.ShellIntegration, historical.ShellIntegration);

        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]9;4;0\x07\x1b]133;A\x07"));
        Assert.AreEqual(TerminalProgressState.Warning, captured.Progress.State);
        Assert.AreEqual(90, historical.Progress.Percentage);
        Assert.AreEqual(TerminalShellIntegrationPhase.Finished, captured.ShellIntegration.Phase);
        Assert.AreEqual(7, historical.ShellIntegration.LastExitCode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RawOutput_ValidAndIgnoredActivity_PreservesPassthrough(bool tokenFiltered)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var presentation = new ActivityCapturePresentation();
        var builder = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithPresentation(presentation).WithDimensions(20, 5);
        if (tokenFiltered)
            builder.AddPresentationFilter(presentation);
        await using var terminal = builder.Build();
        const string output =
            "\u009d9;4;1;80\u009c\x1b]133;D;-3\x1b\\" +
            "\x1b]9;4;1;-1\x07\x1b]133;unsupported\x07\x1b]133;;D;0\x07" + "done";
        workload.Write(output);
        var captured = await presentation.Output.Task.WaitAsync(
            TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var expected = tokenFiltered
            ? AnsiTokenSerializer.Serialize(AnsiTokenizer.Tokenize(output))
            : output;
        Assert.AreEqual(expected, Encoding.UTF8.GetString(captured));
        using var snapshot = await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("done"), TimeSpan.FromSeconds(5), "activity and following text applied")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        Assert.AreEqual(80, snapshot.Progress.Percentage);
        Assert.AreEqual(-3, snapshot.ShellIntegration.LastExitCode);
    }

    private sealed class ActivityCapturePresentation :
        IHex1bTerminalPresentationAdapter, IHex1bTerminalPresentationFilter
    {
        private readonly HeadlessPresentationAdapter _headless = new(20, 5);
        public TaskCompletionSource<byte[]> Output { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Width => _headless.Width;
        public int Height => _headless.Height;
        public TerminalCapabilities Capabilities => _headless.Capabilities;
        public event Action<int, int>? Resized
        {
            add => _headless.Resized += value;
            remove => _headless.Resized -= value;
        }
        public event Action? Disconnected
        {
            add => _headless.Disconnected += value;
            remove => _headless.Disconnected -= value;
        }
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            Output.TrySetResult(data.ToArray());
            return ValueTask.CompletedTask;
        }
        public ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
            => _headless.ReadInputAsync(ct);
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync() => _headless.DisposeAsync();
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask<IReadOnlyList<AnsiToken>> OnOutputAsync(
            IReadOnlyList<AppliedToken> appliedTokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.FromResult<IReadOnlyList<AnsiToken>>(appliedTokens.Select(applied => applied.Token).ToArray());
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
    }
}
