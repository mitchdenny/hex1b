using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class OscProgramStatusTests
{
    [TestMethod]
    [DataRow("idle", TerminalProgramStatusState.Idle)]
    [DataRow("working", TerminalProgramStatusState.Working)]
    [DataRow("blocked", TerminalProgramStatusState.Blocked)]
    [DataRow("done", TerminalProgramStatusState.Done)]
    [DataRow("error", TerminalProgramStatusState.Error)]
    public async Task Report_AllStates_ReplacesRecordWithoutScreenEffects(string text, TerminalProgramStatusState state)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("visible"));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            $"\x1b]7501;state={text}:id=Build/test:kind=permission:progress=007:app=cargo:title=VGVzdA:msg=SGVsbG8=\x07"));
        var record = TestSeq.Single(terminal.ProgramStatus.Records);
        Assert.AreEqual("Build/test", record.Id);
        Assert.AreEqual(state, record.State);
        Assert.AreEqual(state == TerminalProgramStatusState.Blocked ? TerminalProgramStatusKind.Permission : null, record.Kind);
        Assert.AreEqual(state is TerminalProgramStatusState.Working or TerminalProgramStatusState.Blocked ? 7 : null, record.Progress);
        Assert.AreEqual("cargo", record.App);
        Assert.AreEqual("cargo", record.EffectiveApp);
        Assert.AreEqual("Test", record.Title);
        Assert.AreEqual("Hello", record.Message);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreSame(terminal.ProgramStatus, snapshot.ProgramStatus);
        Assert.AreEqual("visible", snapshot.GetText().Trim());
        Assert.AreEqual(7, snapshot.CursorX);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b]7501;state={text}:id=Build/test\x07"));
        var replaced = terminal.ProgramStatus.Find("Build/test")!;
        Assert.IsNull(replaced.App);
        Assert.IsNull(replaced.Title);
        Assert.IsNull(replaced.Message);
        Assert.IsNull(replaced.Kind);
        Assert.IsNull(replaced.Progress);
        Assert.AreEqual("Hello", record.Message);
    }

    [TestMethod]
    [DataRow("state=blocked:kind=permission", TerminalProgramStatusKind.Permission)]
    [DataRow("state=blocked:kind=question", TerminalProgramStatusKind.Question)]
    [DataRow("state=blocked:kind=auth", TerminalProgramStatusKind.Auth)]
    [DataRow("state=blocked:kind=new", null)]
    [DataRow("state=working:kind=auth", null)]
    public async Task Kind_RecognizedAndUnknownValues_UsesBlockedOnly(string body, TerminalProgramStatusKind? expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b]7501;{body}\x07"));
        Assert.AreEqual(expected, terminal.ProgramStatus.Find("")!.Kind);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("100", 100)]
    [DataRow("0001", 1)]
    [DataRow("", null)]
    [DataRow("-1", null)]
    [DataRow("+1", null)]
    [DataRow("101", null)]
    [DataRow("1.0", null)]
    [DataRow("999999999999999999999", null)]
    public async Task Progress_ValidAndInvalidValues_UsesPercentageOrIndeterminate(string value, int? expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens([new OscToken("7501", "", "state=working:progress=" + value)]);
        Assert.AreEqual(expected, terminal.ProgramStatus.Find("")!.Progress);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("app=cargo")]
    [DataRow("state=")]
    [DataRow("state=future")]
    [DataRow("state=Working")]
    [DataRow("state=working:id=")]
    [DataRow("state=working:id=/x")]
    [DataRow("state=working:id=x/")]
    [DataRow("state=working:id=x//y")]
    [DataRow("state=working:id=a,b")]
    [DataRow("state=working:msg=A")]
    [DataRow("state=working:msg=AA==")]
    [DataRow("state=working:msg=wIA=")]
    [DataRow("state=working:msg=/w==")]
    [DataRow("state=working:msg=QQ=")]
    [DataRow("state=working:msg=QQ===")]
    [DataRow("state=working:msg=QQ==QQ==")]
    [DataRow("state=clear:msg=Cg==")]
    [DataRow("state=working:msg=Cg==:msg=SGVsbG8=")]
    [DataRow("state=working:abcdefghijklmnopq=x")]
    [DataRow("state=working:abcdefghijklmnopq")]
    public async Task Report_InvalidInput_DiscardsWholeWithoutMutation(string body)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens([new OscToken("7501", "", "state=done:app=baseline")]);
        var baseline = terminal.ProgramStatus;
        var changes = 0;
        terminal.ProgramStatusChanged += _ => changes++;
        terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b]7501;{body}\x07"));
        Assert.AreSame(baseline, terminal.ProgramStatus, body);
        Assert.AreEqual(0, changes, body);
    }

    [TestMethod]
    public async Task Report_MalformedPairsWhitespaceAndDuplicates_ProcessesRemainingPairs()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]7501;bad:=x:UPPER=x:msg=bad!:app=bad,:unknown=x:state=idle: state = blocked :progress=90:progress=no:kind=auth:kind=question:msg=QQ:msg=Qg==\x07"));
        var record = TestSeq.Single(terminal.ProgramStatus.Records);
        Assert.AreEqual(TerminalProgramStatusState.Blocked, record.State);
        Assert.AreEqual(TerminalProgramStatusKind.Question, record.Kind);
        Assert.IsNull(record.App);
        Assert.IsNull(record.Progress);
        Assert.AreEqual("B", record.Message);
        Assert.AreEqual("", record.Id);
    }

    [TestMethod]
    public async Task Reports_ControlsAndUnicode_RejectsControlsAndInvalidUtf8WithoutPartialMutation()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens([new OscToken("7501", "", "state=done")]);
        var baseline = terminal.ProgramStatus;
        foreach (var key in new[] { "msg", "title" })
        {
            foreach (var control in Enumerable.Range(0, 32).Concat(Enumerable.Range(127, 33)))
            {
                terminal.ApplyTokens([new OscToken("7501", "", "state=clear:" + key + "=" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("text" + (char)control)))]);
                Assert.AreSame(baseline, terminal.ProgramStatus, $"key={key}, control={control}");
            }
        }
        var text = "你好 🚀 café";
        terminal.ApplyTokens([new OscToken("7501", "", "state=done:msg=" +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('='))]);
        Assert.AreEqual(text, terminal.ProgramStatus.Find("")!.Message);
    }

    [TestMethod]
    public async Task Limits_ExactBoundariesAndUnknownPairs_AppliesOnlyWithinAllLimits()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var bodies = new[]
        {
            ("state=done:title=" + Convert.ToBase64String(new byte[193].Select(_ => (byte)'a').ToArray()), false),
            ("state=done:msg=" + Convert.ToBase64String(new byte[2049].Select(_ => (byte)'a').ToArray()), false),
            ("state=done:title=" + new string('a', 257), false),
            ("state=done:msg=" + new string('a', 2733), false),
            ("state=done:app=" + new string('a', 33), false),
            ("state=done:id=" + new string('a', 33), false),
            ("state=done:id=" + string.Join('/', Enumerable.Repeat("a", 9)), false),
            ("state=done:id=" + string.Join('/', Enumerable.Repeat(new string('a', 32), 4)), false),
            ("state=done:id=" + new string('a', 32), true),
            ("state=done:id=" + string.Join('/', Enumerable.Repeat("a", 8)), true),
            ("state=done:id=" + string.Join('/', Enumerable.Repeat(new string('a', 15), 7)) + "/" + new string('a', 16), true),
            ("state=done:app=" + new string('a', 32), true),
            ("state=done:title=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('a', 192))), true),
            ("state=done:msg=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('a', 2048))), true)
        };
        foreach (var (body, valid) in bodies)
        {
            terminal.RestoreProgramStatus(new([]));
            terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b]7501;" + body + "\x07"));
            Assert.AreEqual(valid ? 1 : 0, terminal.ProgramStatus.Records.Count, body[..Math.Min(body.Length, 80)]);
        }
        foreach (var terminator in new[] { "\x07", "\x1b\\" })
        {
            var prefix = "\x1b]7501;state=idle:unknown=";
            var legal = prefix + new string('a', 4096 - prefix.Length - terminator.Length) + terminator;
            terminal.RestoreProgramStatus(new([]));
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(legal));
            Assert.AreEqual(1, terminal.ProgramStatus.Records.Count);
            var baseline = terminal.ProgramStatus;
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(legal.Replace("unknown=", "unknown=a")));
            Assert.AreSame(baseline, terminal.ProgramStatus);
        }
    }

    [TestMethod]
    public async Task Hierarchy_AbsentAncestorsAndClear_UsesOrdinalPathsAndRecomputesApp()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]7501;state=idle:app=root\x07" +
            "\x1b]7501;state=done:id=build/test/result\x07" +
            "\x1b]7501;state=error:id=build/test2\x07" +
            "\x1b]7501;state=blocked:id=Build/test\x07"));
        Assert.AreEqual(2, terminal.ProgramStatus.GetDescendants("build").Count);
        Assert.AreEqual(3, terminal.ProgramStatus.GetDescendants("").Count);
        Assert.AreEqual("root", terminal.ProgramStatus.Find("build/test/result")!.EffectiveApp);
        terminal.ApplyTokens([new OscToken("7501", "", "state=working:id=build:app=cargo")]);
        Assert.AreEqual("cargo", terminal.ProgramStatus.Find("build/test/result")!.EffectiveApp);
        var before = terminal.ProgramStatus;
        terminal.ApplyTokens([new OscToken("7501", "", "state=idle:id=build")]);
        Assert.AreEqual("root", terminal.ProgramStatus.Find("build/test/result")!.EffectiveApp);
        Assert.AreEqual("cargo", before.Find("build/test/result")!.EffectiveApp);
        terminal.ApplyTokens([new OscToken("7501", "", "state=clear:id=build/test")]);
        Assert.IsNull(terminal.ProgramStatus.Find("build/test/result"));
        Assert.IsNotNull(terminal.ProgramStatus.Find("build/test2"));
        Assert.IsNotNull(terminal.ProgramStatus.Find("Build/test"));
        terminal.ApplyTokens([new OscToken("7501", "", "state=clear:id=build")]);
        Assert.IsNull(terminal.ProgramStatus.Find("build/test2"));
        terminal.ApplyTokens([new OscToken("7501", "", "state=clear")]);
        Assert.AreEqual(0, terminal.ProgramStatus.Records.Count);
    }

    [TestMethod]
    public async Task Inventory_CapacityUpdatesAndRestore_EvictsLeastRecentlyUpdatedRecordOnly()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        for (var i = 0; i < 256; i++)
            terminal.ApplyTokens([new OscToken("7501", "", $"state=done:id=r{i}")]);
        terminal.ApplyTokens([new OscToken("7501", "", "state=done:id=r0")]);
        var saved = terminal.ProgramStatus;
        terminal.ApplyTokens([new OscToken("7501", "", "state=done:id=new")]);
        Assert.AreEqual(256, terminal.ProgramStatus.Records.Count);
        Assert.IsNull(terminal.ProgramStatus.Find("r1"));
        Assert.IsNotNull(terminal.ProgramStatus.Find("r0"));
        Assert.IsNotNull(terminal.ProgramStatus.Find("r2"));
        terminal.RestoreProgramStatus(saved);
        terminal.ApplyTokens([new OscToken("7501", "", "state=done:id=new")]);
        Assert.IsNull(terminal.ProgramStatus.Find("r1"));
        Assert.AreEqual("r2", terminal.ProgramStatus.Records[0].Id);
    }

    [TestMethod]
    public async Task Inventory_AncestorEviction_RecomputesDescendantsWithoutRemovingThem()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens([new OscToken("7501", "", "state=done:id=build:app=cargo"),
            new OscToken("7501", "", "state=working:id=build/test"),
            new OscToken("7501", "", "state=idle:app=root")]);
        for (var i = 0; i < 253; i++)
            terminal.ApplyTokens([new OscToken("7501", "", $"state=done:id=r{i}")]);
        var before = terminal.ProgramStatus;
        terminal.ApplyTokens([new OscToken("7501", "", "state=done:id=extra")]);
        Assert.IsNull(terminal.ProgramStatus.Find("build"));
        Assert.AreEqual("root", terminal.ProgramStatus.Find("build/test")!.EffectiveApp);
        Assert.AreEqual("cargo", before.Find("build/test")!.EffectiveApp);
        Assert.AreEqual(256, terminal.ProgramStatus.Records.Count);
    }

    [TestMethod]
    [DataRow("\x1b[!p", false)]
    [DataRow("\x1b[2J", false)]
    [DataRow("\x1b[?1049h\x1b[?1049l", false)]
    [DataRow("\x1b]133;B\x07", false)]
    [DataRow("\x1b]133;D;0\x07", false)]
    [DataRow("\x1b]133;A\x07", true)]
    public async Task Lifecycle_PromptOnlyExpiresTransientRecords(string sequence, bool expires)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        foreach (var state in Enum.GetValues<TerminalProgramStatusState>())
            terminal.ApplyTokens([new OscToken("7501", "", $"state={state.ToString().ToLowerInvariant()}:id={state}")]);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(sequence));
        Assert.AreEqual(expires ? 3 : 5, terminal.ProgramStatus.Records.Count);
        Assert.IsNotNull(terminal.ProgramStatus.Find("Done"));
        Assert.IsNotNull(terminal.ProgramStatus.Find("Error"));
        Assert.IsNotNull(terminal.ProgramStatus.Find("Idle"));
        terminal.ApplyTokens([new OscToken("9", "4", "1;50")]);
        Assert.IsNull(terminal.ProgramStatus.Find(""));
        terminal.ApplyTokens([RisToken.Instance]);
        Assert.AreEqual(0, terminal.ProgramStatus.Records.Count);
    }

    [TestMethod]
    public async Task Restore_CompleteInventoryAndNestedSuppression_ObserversSeeOneCoherentCommit()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        var changes = new List<TerminalProgramStatusSnapshot>();
        terminal.ProgramStatusChanged += snapshot =>
        {
            Assert.AreSame(snapshot, terminal.ProgramStatus);
            using var screen = terminal.CreateSnapshot();
            Assert.AreSame(snapshot, screen.ProgramStatus);
            Assert.AreEqual(TerminalShellIntegrationPhase.Prompt, screen.ShellIntegration.Phase);
            changes.Add(snapshot);
        };
        terminal.BeginActivityStateRestore();
        terminal.BeginActivityStateRestore();
        terminal.ApplyTokens([new OscToken("7501", "", "state=working")]);
        terminal.ApplyTokens([new OscToken("133", "", "A")]);
        terminal.RestoreProgramStatus(new([new("child", TerminalProgramStatusState.Blocked)]));
        Assert.AreEqual(0, changes.Count);
        terminal.EndActivityStateRestore();
        Assert.AreEqual(0, changes.Count);
        terminal.EndActivityStateRestore();
        Assert.AreEqual(1, changes.Count);
        Assert.AreEqual(TerminalProgramStatusState.Blocked, terminal.ProgramStatus.Find("child")!.State);
        terminal.RestoreProgramStatus(new([]));
        Assert.AreEqual(2, changes.Count);
        Assert.AreEqual(0, terminal.ProgramStatus.Records.Count);
        terminal.RestoreProgramStatus(new([]));
        Assert.AreEqual(2, changes.Count);
    }

    [TestMethod]
    [DataRow("\x1b]", "\x07")]
    [DataRow("\x1b]", "\x1b\\")]
    [DataRow("\u009d", "\u009c")]
    public async Task RawOutput_EveryByteSplit_PublishesOnlyCompleteReport(string introducer, string terminator)
    {
        var bytes = Encoding.UTF8.GetBytes(introducer + "7501;state=blocked:id=worker:kind=auth:msg=8J+agA==" + terminator);
        for (var split = 1; split < bytes.Length; split++)
        {
            using var workload = new Hex1bAppWorkloadAdapter();
            await using var terminal = Hex1bTerminal.CreateBuilder()
                .WithWorkload(workload).WithHeadless().WithDimensions(40, 5).Build();
            var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            terminal.ProgramStatusChanged += _ => changed.TrySetResult();
            workload.Write(bytes.AsMemory(0, split));
            workload.Write(bytes.AsMemory(split));
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.AreEqual("🚀", terminal.ProgramStatus.Find("worker")!.Message);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual("", snapshot.GetText().Trim());
            Assert.AreEqual(0, snapshot.CursorX);
        }
    }

    [TestMethod]
    [DataRow("\x07")]
    [DataRow("\x1b\\")]
    [DataRow("\x18")]
    [DataRow("\x1a")]
    [DataRow("")]
    public async Task RawOutput_OversizedUnterminatedReports_RemainsBoundedAndRecoversWithoutScreenLeak(string end)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        var filter = new ObservingFilter(() =>
        {
            if (Interlocked.Increment(ref readCount) == 33)
                observed.TrySetResult();
        });
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithHeadless().WithDimensions(40, 5).AddWorkloadFilter(filter).Build();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.ProgramStatusChanged += _ => recovered.TrySetResult();
        workload.Write(Encoding.ASCII.GetBytes("\x1b]7501;state=working:unknown="));
        for (var i = 0; i < 32; i++)
            workload.Write(Encoding.ASCII.GetBytes(new string('a', 4096)));
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.IsTrue(terminal.CapturePendingAnsiOutput().Length <= TerminalProgramStatusCodec.MaximumSequenceBytes + 5);
        Assert.AreEqual(0, terminal.ProgramStatus.Records.Count);
        workload.Write(Encoding.ASCII.GetBytes(end + "\x1b]7501;state=done\x07"));
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.AreEqual(TerminalProgramStatusState.Done, terminal.ProgramStatus.Find("")!.State);
        Assert.IsTrue(terminal.CapturePendingAnsiOutput().Length <= TerminalProgramStatusCodec.MaximumSequenceBytes + 5);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("", snapshot.GetText().Trim());
    }

    [TestMethod]
    [DataRow("\x18")]
    [DataRow("\x1a")]
    [DataRow("\x1b")]
    public async Task RawAndTokenOutput_CancelledReport_DoesNotApplyPartialStateAndRecovers(string cancel)
    {
        foreach (var raw in new[] { false, true })
        {
            using var workload = new Hex1bAppWorkloadAdapter();
            await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
            var sequence = "\x1b]7501;state=error" + cancel + (cancel == "\x1b" ? "]7501;" : "\x1b]7501;") + "state=done\x07";
            if (raw)
            {
                var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                terminal.ProgramStatusChanged += _ => signal.TrySetResult();
                foreach (var value in Encoding.ASCII.GetBytes(sequence))
                    workload.Write(new[] { value });
                await signal.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            else terminal.ApplyTokens(AnsiTokenizer.Tokenize(sequence));
            Assert.AreEqual(TerminalProgramStatusState.Done, terminal.ProgramStatus.Find("")!.State);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual("", snapshot.GetText().Trim());
        }
    }

    [TestMethod]
    public async Task FeatureDetection_RawAndTokenQueries_RespondsWithFixedCapabilitiesOnly()
    {
        foreach (var raw in new[] { false, true })
        {
            await using var workload = new QueryWorkload();
            await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
            if (raw) workload.Output.Write("\x1b]7501;?\x07"u8.ToArray());
            else terminal.ApplyTokens([new OscToken("7501", "", "?")]);
            var reply = await workload.Replies.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("\x1b]7501;?:states=idle,working,done,blocked,error:kinds=permission,question,auth\x1b\\",
                Encoding.ASCII.GetString(reply));
            Assert.AreEqual(0, terminal.ProgramStatus.Records.Count);
        }
    }

    [TestMethod]
    [DataRow("\x1b]", "\x07")]
    [DataRow("\x1b]", "\x1b\\")]
    [DataRow("\u009d", "\u009c")]
    public async Task FeatureDetection_EveryByteSplit_EmitsOneResponseAndNoRecord(string introducer, string terminator)
    {
        var bytes = Encoding.UTF8.GetBytes(introducer + "7501;?" + terminator);
        for (var split = 1; split < bytes.Length; split++)
        {
            await using var workload = new QueryWorkload();
            await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
            workload.Output.Write(bytes.AsMemory(0, split));
            workload.Output.Write(bytes.AsMemory(split));
            var reply = await workload.Replies.Reader.ReadAsync(TestContext.Current.CancellationToken)
                .AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("\x1b]7501;?:states=idle,working,done,blocked,error:kinds=permission,question,auth\x1b\\",
                Encoding.ASCII.GetString(reply));
            Assert.IsFalse(workload.Replies.Reader.TryRead(out _));
            Assert.AreEqual(0, terminal.ProgramStatus.Records.Count);
        }
    }

    [TestMethod]
    public async Task Serialization_DeterministicRandomRecords_RoundTripsAllSupportedFields()
    {
        var random = new Random(7501);
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        for (var i = 0; i < 500; i++)
        {
            var state = (TerminalProgramStatusState)random.Next(5);
            var record = new TerminalProgramStatusRecord("r" + random.Next(300), state,
                state == TerminalProgramStatusState.Blocked ? (TerminalProgramStatusKind)random.Next(3) : null,
                state is TerminalProgramStatusState.Working or TerminalProgramStatusState.Blocked ? random.Next(101) : null,
                random.Next(2) == 0 ? "test-app" : null, "Test 🧪 " + i, new string('a', random.Next(30)));
            terminal.ApplyTokens(AnsiTokenizer.Tokenize(TerminalProgramStatusCodec.Serialize(record)));
            Assert.AreEqual(record with { EffectiveApp = record.App }, terminal.ProgramStatus.Find(record.Id));
            Assert.IsTrue(terminal.ProgramStatus.Records.Count <= 256);
        }
    }

    [TestMethod]
    public async Task LocalProcessExit_DrainsReportsBeforeExpiringOnlyTransientInventory()
    {
        if (OperatingSystem.IsWindows())
            Assert.Inconclusive("This literal printf fixture uses /bin/sh.");
        var info = new ProcessStartInfo("/bin/sh");
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("printf '\\033]7501;state=working:id=work\\007\\033]7501;state=blocked:id=wait\\007\\033]7501;state=done:id=done\\007\\033]7501;state=error:id=error\\007\\033]7501;state=idle:id=idle\\007'");
        await using var workload = new StandardProcessWorkloadAdapter(info);
        await workload.StartAsync(TestContext.Current.CancellationToken);
        await workload.WaitForExitAsync(TestContext.Current.CancellationToken);
        await using var terminal = new Hex1bTerminal(new Hex1bTerminalOptions
        {
            WorkloadAdapter = workload, PresentationAdapter = new HeadlessPresentationAdapter(40, 5),
            Width = 40, Height = 5, RunCallback = _ => Task.FromResult(workload.ExitCode)
        });
        await terminal.RunAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5));
        TestSeq.AreEqual(new[] { "done", "error", "idle" }, terminal.ProgramStatus.Records.Select(record => record.Id));
    }

    [TestMethod]
    public async Task Snapshot_ConcurrentAtomicTokenBatches_PreservesProgramAndActivityPair()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens([new OscToken("7501", "", "state=working:progress=0"), new OscToken("9", "4", "1;0")]);
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
                terminal.ApplyTokens([new OscToken("7501", "", $"state=working:progress={i % 2}"),
                    new OscToken("9", "4", $"1;{i % 2}")]);
        }, TestContext.Current.CancellationToken);
        for (var i = 0; i < 1000; i++)
        {
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual(snapshot.ProgramStatus.Find("")!.Progress, snapshot.Progress.Percentage);
        }
        await writer.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [TestMethod]
    public void Snapshot_CopiesAndProtectsInventory_RejectsMutationAndInvalidInventory()
    {
        var source = new List<TerminalProgramStatusRecord> { new("a", TerminalProgramStatusState.Idle) };
        var snapshot = new TerminalProgramStatusSnapshot(source);
        source.Clear();
        Assert.AreEqual(1, snapshot.Records.Count);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<TerminalProgramStatusRecord>)snapshot.Records).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<TerminalProgramStatusRecord>)snapshot.GetDescendants("")).Clear());
        Assert.ThrowsExactly<ArgumentNullException>(() => snapshot.Find(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => snapshot.GetDescendants(null!));
        Assert.ThrowsExactly<ArgumentException>(() => new TerminalProgramStatusSnapshot(
            [new("a", TerminalProgramStatusState.Idle), new("a", TerminalProgramStatusState.Done)]));
        Assert.ThrowsExactly<ArgumentException>(() => new TerminalProgramStatusSnapshot(
            Enumerable.Range(0, 257).Select(i => new TerminalProgramStatusRecord(i.ToString(), TerminalProgramStatusState.Done))));
    }

    [TestMethod]
    public void Codec_InvalidRecords_ThrowsBeforeEmission()
    {
        var valid = new TerminalProgramStatusRecord("", TerminalProgramStatusState.Working);
        foreach (var record in new[]
        {
            valid with { State = (TerminalProgramStatusState)99 },
            valid with { State = TerminalProgramStatusState.Blocked, Kind = (TerminalProgramStatusKind)99 },
            valid with { Kind = TerminalProgramStatusKind.Auth },
            valid with { State = TerminalProgramStatusState.Idle, Progress = 1 },
            valid with { Progress = -1 },
            valid with { Progress = 101 },
            valid with { Id = "a//b" },
            valid with { Id = new string('a', 129) },
            valid with { App = "" },
            valid with { App = "bad app" },
            valid with { App = new string('a', 33) },
            valid with { Title = new string('a', 193) },
            valid with { Message = new string('a', 2049) },
            valid with { Title = "\u0080" },
            valid with { Message = "\n" },
            valid with { Message = "\ud800" }
        })
            Assert.ThrowsExactly<ArgumentException>(() => TerminalProgramStatusCodec.Serialize(record));
        Assert.ThrowsExactly<ArgumentException>(() => TerminalProgramStatusCodec.SerializeClear("bad//id"));
        Assert.AreEqual("\x1b]7501;state=clear\x1b\\", TerminalProgramStatusCodec.SerializeClear(null));
        Assert.AreEqual("\x1b]7501;state=clear:id=a/b\x1b\\", TerminalProgramStatusCodec.SerializeClear("a/b"));
    }

    [TestMethod]
    public async Task Queries_UpstreamOwnershipAndUnsolicitedReplies_DoesNotSynthesizeOrReflectInput()
    {
        await using var workload = new QueryWorkload { HandlesProtocolQueries = true };
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        terminal.ApplyTokens(AnsiTokenizer.Tokenize(
            "\x1b]7501;?\x07\x1b]7501;?:states=idle\x07\x1b]7501;state=error:msg=SG9zdGlsZQ==\x07"));
        Assert.IsFalse(workload.Replies.Reader.TryRead(out _));
        Assert.AreEqual(TerminalProgramStatusState.Error, terminal.ProgramStatus.Find("")!.State);
    }

    [TestMethod]
    [DataRow(10)]
    [DataRow(10000)]
    public async Task RawOutput_DcsRecoveryBoundary_DoesNotConsumeFollowingScreenContent(int payloadSize)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        await using var terminal = Hex1bTerminal.CreateBuilder().WithWorkload(workload).WithHeadless().Build();
        workload.Write(Encoding.ASCII.GetBytes("\x1b]7501;state=working:unknown=" + new string('a', payloadSize)));
        workload.Write("\x1bPq\x1b\\visible"u8.ToArray());
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(snapshot => snapshot.ContainsText("visible"), TimeSpan.FromSeconds(5), "screen content after DCS boundary")
            .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
        Assert.AreEqual(0, terminal.ProgramStatus.Records.Count);
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual("visible", snapshot.GetText().Trim());
    }

    [TestMethod]
    public async Task RawOutput_DeterministicRandomChunks_MatchesTokenPathInventoryAndEviction()
    {
        using var tokenWorkload = new Hex1bAppWorkloadAdapter();
        using var rawWorkload = new Hex1bAppWorkloadAdapter();
        await using var tokenTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(tokenWorkload).WithHeadless().Build();
        await using var rawTerminal = Hex1bTerminal.CreateBuilder().WithWorkload(rawWorkload).WithHeadless().Build();
        var random = new Random(750104);
        var wire = new StringBuilder();
        for (var i = 0; i < 600; i++)
        {
            var id = "worker/" + random.Next(320);
            var state = (TerminalProgramStatusState)random.Next(5);
            var record = new TerminalProgramStatusRecord(id, state, App: random.Next(3) == 0 ? "build" : null,
                Title: $"Worker {i}", Message: "🧪");
            wire.Append(TerminalProgramStatusCodec.Serialize(record));
            if (i % 7 == 0) wire.Append(TerminalProgramStatusCodec.SerializeClear(id));
            if (i % 13 == 0) wire.Append("\x1b]133;A\x07");
            if (i % 11 == 0) wire.Append("\x1b]7501;state=error:msg=AA==\x07");
        }
        wire.Append("\x1b]7501;state=done:id=sentinel\x07");
        tokenTerminal.ApplyTokens(AnsiTokenizer.Tokenize(wire.ToString()));
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rawTerminal.ProgramStatusChanged += snapshot =>
        {
            if (snapshot.Find("sentinel") is not null) finished.TrySetResult();
        };
        var bytes = Encoding.UTF8.GetBytes(wire.ToString());
        for (var offset = 0; offset < bytes.Length;)
        {
            var size = Math.Min(random.Next(1, 128), bytes.Length - offset);
            rawWorkload.Write(bytes.AsMemory(offset, size));
            offset += size;
        }
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        TestSeq.AreEqual(tokenTerminal.ProgramStatus.Records, rawTerminal.ProgramStatus.Records);
        using var screen = rawTerminal.CreateSnapshot();
        Assert.AreEqual("", screen.GetText().Trim());
    }

    [TestMethod]
    [DataRow("\x07", false)]
    [DataRow("\x1b\\", false)]
    [DataRow("\\", true)]
    public async Task PendingAnsi_OversizedDiscardContinuation_RecreatesStateWithoutLeakingBody(string terminator, bool holdEscape)
    {
        using var producerWorkload = new Hex1bAppWorkloadAdapter();
        using var replicaWorkload = new Hex1bAppWorkloadAdapter();
        var producerRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replicaRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var producer = Hex1bTerminal.CreateBuilder().WithWorkload(producerWorkload).WithHeadless()
            .AddWorkloadFilter(new ObservingFilter(() => producerRead.TrySetResult())).Build();
        await using var replica = Hex1bTerminal.CreateBuilder().WithWorkload(replicaWorkload).WithHeadless()
            .AddWorkloadFilter(new ObservingFilter(() => replicaRead.TrySetResult())).Build();
        var initial = "\x1b]7501;state=working:unknown=" + new string('a', 10000) + (holdEscape ? "\x1b" : "");
        producerWorkload.Write(Encoding.ASCII.GetBytes(initial));
        await producerRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var pending = producer.CapturePendingAnsiOutput();
        Assert.AreEqual(TerminalProgramStatusCodec.MaximumSequenceBytes + 1 + (holdEscape ? 1 : 0), pending.Length);
        replicaWorkload.Write(pending);
        await replicaRead.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var continuation = (holdEscape ? "" : "discarded-tail") + terminator + "visible";
        producerWorkload.Write(Encoding.ASCII.GetBytes(continuation));
        replicaWorkload.Write(Encoding.ASCII.GetBytes(continuation));
        foreach (var terminal in new[] { producer, replica })
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(snapshot => snapshot.ContainsText("visible"), TimeSpan.FromSeconds(5), "continued oversized OSC terminated")
                .Build().ApplyAsync(terminal, TestContext.Current.CancellationToken);
            using var snapshot = terminal.CreateSnapshot();
            Assert.AreEqual("visible", snapshot.GetText().Trim());
            Assert.AreEqual(0, snapshot.ProgramStatus.Records.Count);
            Assert.AreEqual(0, terminal.CapturePendingAnsiOutput().Length);
        }
    }

    private sealed class ObservingFilter(Action observe) : IHex1bTerminalWorkloadFilter
    {
        public ValueTask OnOutputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
        { observe(); return ValueTask.CompletedTask; }
        public ValueTask OnSessionStartAsync(int width, int height, DateTimeOffset timestamp, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnFrameCompleteAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask OnInputAsync(IReadOnlyList<AnsiToken> tokens, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnResizeAsync(int width, int height, TimeSpan elapsed, CancellationToken ct = default)
            => ValueTask.CompletedTask;
        public ValueTask OnSessionEndAsync(TimeSpan elapsed, CancellationToken ct = default) => ValueTask.CompletedTask;
    }

    private sealed class QueryWorkload : IHex1bTerminalWorkloadAdapter
    {
        internal Hex1bAppWorkloadAdapter Output { get; } = new();
        internal Channel<byte[]> Replies { get; } = Channel.CreateUnbounded<byte[]>();
        public bool HandlesProtocolQueries { get; init; }
        public event Action? Disconnected { add => Output.Disconnected += value; remove => Output.Disconnected -= value; }
        public ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default) => Output.ReadOutputAsync(ct);
        public ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
            => Replies.Writer.WriteAsync(data.ToArray(), ct);
        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default)
            => Output.ResizeAsync(width, height, ct);
        public ValueTask DisposeAsync() { Output.Dispose(); return ValueTask.CompletedTask; }
    }
}
