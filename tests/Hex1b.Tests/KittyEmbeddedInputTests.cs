using System.Text;
using System.Threading.Channels;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Tokens;
using Hex1b.Widgets;

namespace Hex1b.Tests;

[TestClass]
public class KittyEmbeddedInputTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RouteInput_ChildNegotiationAndCopyMode_OnlyReleasesForwardedPresses(bool childEnhanced)
    {
        await using var childWorkload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        if (childEnhanced) child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var copyEvents = new List<Hex1bEvent>();
        handle.CopyModeInput += evt =>
        {
            copyEvents.Add(evt);
            if (evt is Hex1bKeyEvent { Key: Hex1bKey.F6 })
            {
                handle.EnterCopyMode();
                return true;
            }
            return handle.IsInCopyMode;
        };
        var actions = 0;
        node.BindingsConfigurator = b => b.Key(Hex1bKey.B).Action(() => actions++);

        var entry = Hex1bKeyEvent.Plain(Hex1bKey.F6);
        await InputRouter.RouteInputAsync(node, entry, ring, state);
        await InputRouter.RouteInputAsync(node, entry with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.IsTrue(handle.IsInCopyMode);
        Assert.HasCount(1, copyEvents, "Releases must never execute copy-mode actions.");
        handle.ExitCopyMode();

        var shortcut = Hex1bKeyEvent.Plain(Hex1bKey.B);
        await InputRouter.RouteInputAsync(node, shortcut, ring, state);
        await InputRouter.RouteInputAsync(node, shortcut with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.AreEqual(1, actions);
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "Intercepted presses must not produce child releases.");

        handle.EnterCopyMode();
        var intercepted = Hex1bKeyEvent.Plain(Hex1bKey.C, 'c');
        await InputRouter.RouteInputAsync(node, intercepted, ring, state);
        handle.ExitCopyMode();
        await InputRouter.RouteInputAsync(node, intercepted with { EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "A press consumed inside copy mode must not escape on release.");

        var press = new Hex1bKeyEvent(Hex1bKey.A, "a", Hex1bModifiers.None) { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(node, press, ring, state);
        handle.EnterCopyMode();
        await InputRouter.RouteInputAsync(node, press with { Text = "", EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.AreEqual(childEnhanced ? "\x1b[97u" : "a", await childWorkload.ReadInputAsync());
        if (childEnhanced) Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
        Assert.HasCount(3, copyEvents, "Only F6, the copy-mode C press and the forwarded A press should reach the copy handler.");
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_EmbeddedBackpressure_PreservesPressRepeatReleaseOrder(bool enhancedHost)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var childWorkload = new GatedWorkload { BlockFirstWrite = true };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        Hex1bAppWorkloadAdapter? appInput = null;
        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new VStackWidget([
                    new TextBlockWidget("Embedded ready"),
                    new TerminalWidget(handle)
                ]).InputBindings(b => b.Key(Hex1bKey.F8).Global().OverridesCapture().Action(() => drained.TrySetResult())))
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Embedded ready"), TimeSpan.FromSeconds(5), "embedded app ready")
                .Build().ApplyAsync(host);
            if (enhancedHost) host.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>31u"));
            Assert.AreEqual(enhancedHost ? 31 : 0, host.InputModes.KittyKeyboardFlags);
            Assert.AreEqual(11, child.InputModes.KittyKeyboardFlags);
            var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a');
            appInput!.TryWriteInputEvent(press);
            await childWorkload.FirstWrite.Task.WaitAsync(TimeSpan.FromSeconds(5));
            appInput.TryWriteInputEvent(press with { EventType = Hex1bKeyEventType.Repeat });
            appInput.TryWriteInputEvent(press with { Text = "", EventType = Hex1bKeyEventType.Release });
            appInput.SendKey(Hex1bKey.F8);
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
            Assert.IsFalse(drained.Task.IsCompleted);
            childWorkload.Unblock.TrySetResult();
            await drained.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("\x1b[97u", await childWorkload.ReadInputAsync());
            Assert.AreEqual("\x1b[97;1:2u", await childWorkload.ReadInputAsync());
            Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
        }
        finally
        {
            childWorkload.Unblock.TrySetResult();
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_EmbeddedWriteFailure_IsNotMistakenForClosedAppInput(bool channelClosed)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Exception expected = channelClosed ? new ChannelClosedException() : new IOException("child write failed");
        await using var workload = new GatedWorkload { WriteFailure = expected };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new VStackWidget([new TextBlockWidget("Write failure ready"), new TerminalWidget(handle)]))
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Write failure ready"), TimeSpan.FromSeconds(5), "embedded failure app ready")
                .Build().ApplyAsync(host);
            appInput!.SendKey(Hex1bKey.A, 'a');
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Fail("Embedded write failure must propagate from the app.");
            }
            catch (Exception ex) when (ReferenceEquals(ex, expected)) { }
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception ex) when (ReferenceEquals(ex, expected)) { }
        }
    }

    [TestMethod]
    public async Task RouteInput_EmbeddedWriteFailure_PropagatesToCaller()
    {
        var expected = new IOException("child write failed");
        await using var workload = new GatedWorkload { WriteFailure = expected };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var error = await Assert.ThrowsAsync<IOException>(() => InputRouter.RouteInputAsync(
            node, Hex1bKeyEvent.Plain(Hex1bKey.A, 'a'), ring, new InputRouterState()));
        Assert.AreSame(expected, error);
    }

    [TestMethod]
    [DataRow("ancestor")]
    [DataRow("global")]
    public async Task RouteInput_ReleaseOfPressConsumedByBinding_BubblesToCustomHandlerWithoutChildRelease(string binding)
    {
        await using var childWorkload = new GatedWorkload();
        await using var child = CreateEnhancedChild(childWorkload, out var handle);
        var terminal = new TerminalNode { Handle = handle, IsFocused = true };
        var root = new RecordingContainerNode(terminal);
        var actions = 0;
        root.BindingsConfig = b =>
        {
            var step = b.Key(Hex1bKey.B);
            if (binding == "global") step.Global().Action(() => actions++);
            else step.Action(() => actions++);
        };
        var ring = new FocusRing();
        ring.Rebuild(root);
        var state = new InputRouterState();

        var press = Hex1bKeyEvent.Plain(Hex1bKey.B, 'b');
        await InputRouter.RouteInputAsync(root, press, ring, state);
        var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };
        await InputRouter.RouteInputAsync(root, release, ring, state);

        Assert.AreEqual(1, actions, "The release must not execute the binding again.");
        Assert.AreSame(release, TestSeq.Single(root.ReceivedInputs), "Custom ancestor handlers must still see the release.");
        Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "The child never received the press, so it must not get a release.");
    }

    [TestMethod]
    [DataRow("focus")]
    [DataRow("capture")]
    [DataRow("remove")]
    [DataRow("replace-handle")]
    public async Task RouteInput_HandoffBeforeRelease_ReleasesOriginalChildExactlyOnce(string handoff)
    {
        await using var firstWorkload = new GatedWorkload();
        await using var first = CreateEnhancedChild(firstWorkload, out var firstHandle);
        await using var secondWorkload = new GatedWorkload();
        await using var second = CreateEnhancedChild(secondWorkload, out var secondHandle);
        await using var thirdWorkload = new GatedWorkload();
        await using var third = CreateEnhancedChild(thirdWorkload, out var thirdHandle);
        var firstNode = new TerminalNode { Handle = firstHandle };
        var secondNode = new TerminalNode { Handle = secondHandle };
        var root = new RecordingContainerNode(firstNode, secondNode);
        var ring = new FocusRing();
        ring.Rebuild(root);
        ring.Focus(firstNode);
        var state = new InputRouterState();

        // Shift is held for the press but already lifted by the release; identity ignores modifiers.
        var press = new Hex1bKeyEvent(Hex1bKey.A, "A", Hex1bModifiers.Shift) { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(root, press, ring, state);
        Assert.AreEqual("\x1b[97;2u", await firstWorkload.ReadInputAsync());

        switch (handoff)
        {
            case "focus":
                ring.Focus(secondNode);
                break;
            case "capture":
                ring.CaptureInput(secondNode);
                break;
            case "remove":
                root.Children.Remove(firstNode);
                firstNode.Parent = null;
                ring.Rebuild(root);
                ring.Focus(secondNode);
                break;
            case "replace-handle":
                firstNode.Handle = thirdHandle;
                break;
        }

        var release = new Hex1bKeyEvent(Hex1bKey.A, "", Hex1bModifiers.None)
        {
            UnicodeKeyCode = 97,
            EventType = Hex1bKeyEventType.Release
        };
        Assert.AreEqual(InputResult.Handled, await InputRouter.RouteInputAsync(root, release, ring, state));
        Assert.AreEqual("\x1b[97;1:3u", await firstWorkload.ReadInputAsync());

        await InputRouter.RouteInputAsync(root, release, ring, state);
        Assert.IsFalse(firstWorkload.Input.Reader.TryRead(out _), "The original child is released exactly once.");
        Assert.IsFalse(secondWorkload.Input.Reader.TryRead(out _), "The new focus target never saw the press.");
        Assert.IsFalse(thirdWorkload.Input.Reader.TryRead(out _), "A replacement handle never saw the press.");
        Assert.HasCount(1, root.ReceivedInputs, "Only the unmatched duplicate release bubbles to custom handlers.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RouteInput_ChildChangesEventReportingMidHold_UsesCurrentModeEncoding(bool enable)
    {
        await using var workload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        if (!enable) child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();

        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(node, press, ring, state);
        Assert.AreEqual(enable ? "a" : "\x1b[97u", await workload.ReadInputAsync());

        child.ApplyTokens(AnsiTokenizer.Tokenize(enable ? "\x1b[>11u" : "\x1b[<u"));
        await InputRouter.RouteInputAsync(node, press with { EventType = Hex1bKeyEventType.Repeat }, ring, state);
        Assert.AreEqual(enable ? "\x1b[97;1:2u" : "a", await workload.ReadInputAsync());
        await InputRouter.RouteInputAsync(node, press with { Text = "", EventType = Hex1bKeyEventType.Release }, ring, state);
        if (enable)
            Assert.AreEqual("\x1b[97;1:3u", await workload.ReadInputAsync());
        Assert.IsFalse(workload.Input.Reader.TryRead(out _), "Legacy mode has no release encoding.");
        Assert.IsFalse(handle.HasAnyForwardedKeys);
    }

    [TestMethod]
    public async Task RouteInput_MoreHeldKeysThanLegacyBound_ReleasesEachForwardedPressOnce()
    {
        await using var workload = new GatedWorkload();
        await using var child = CreateEnhancedChild(workload, out var handle);
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        const int held = 40;
        var presses = Enumerable.Range(0, held).Select(i => Hex1bKeyEvent.Plain(Hex1bKey.None) with
        {
            Text = char.ConvertFromUtf32(0x3041 + i),
            UnicodeKeyCode = 0x3041 + i
        }).ToArray();

        foreach (var press in presses)
        {
            await InputRouter.RouteInputAsync(node, press, ring, state);
            Assert.AreEqual($"\x1b[{press.UnicodeKeyCode}u", await workload.ReadInputAsync());
        }
        Assert.AreEqual(held, handle.ForwardedKeyCount, "No genuinely held press is evicted.");

        // Release the oldest first: it must not have been dropped by any bound.
        foreach (var press in presses)
        {
            var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };
            Assert.AreEqual(InputResult.Handled, await InputRouter.RouteInputAsync(node, release, ring, state));
            Assert.AreEqual($"\x1b[{press.UnicodeKeyCode};1:3u", await workload.ReadInputAsync());
        }
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.IsFalse(handle.HasAnyForwardedKeys);
    }

    private static Hex1bKeyEvent StructuredPress(int code)
        => Hex1bKeyEvent.FromText(char.ConvertFromUtf32(code)) with { UnicodeKeyCode = code };

    private static Hex1bKeyEvent StructuredRelease(int code)
        => Hex1bKeyEvent.FromText("") with { UnicodeKeyCode = code, EventType = Hex1bKeyEventType.Release };

    [TestMethod]
    [DataRow(null, true)]
    [DataRow(null, false)]
    [DataRow(false, true)]
    public async Task RouteInput_SourceWithoutReleaseReporting_PastTrackingLimit_DropsStaleBookkeepingWithoutRelease(
        bool? sourceReportsReleases, bool childEnhanced)
    {
        await using var workload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        if (childEnhanced)
            child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState { SourceReportsReleases = sourceReportsReleases };
        // A structured source that supplies distinct identities but never reports releases.
        var codes = Enumerable.Range(0x4E00, TerminalWidgetHandle.MaxForwardedKeys + 40).ToArray();

        foreach (var code in codes)
        {
            await InputRouter.RouteInputAsync(node, StructuredPress(code), ring, state);
            Assert.AreEqual(childEnhanced ? $"\x1b[{code}u" : char.ConvertFromUtf32(code), await workload.ReadInputAsync(),
                "Only the press is written: dropping stale bookkeeping never synthesizes a release.");
        }
        Assert.AreEqual(TerminalWidgetHandle.MaxForwardedKeys, handle.ForwardedKeyCount);
        Assert.IsFalse(handle.OwnsForwardedKey((Hex1bKey.None, codes[39])));
        Assert.IsTrue(handle.OwnsForwardedKey((Hex1bKey.None, codes[40])));

        await state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
        if (childEnhanced)
        {
            foreach (var code in codes.Skip(40))
                Assert.AreEqual($"\x1b[{code};1:3u", await workload.ReadInputAsync());
        }
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.IsFalse(handle.HasAnyForwardedKeys);
    }

    [TestMethod]
    public async Task RouteInput_ReleaseReportingSource_AtTrackingLimit_RejectsWithoutReleasingLiveKeys()
    {
        await using var workload = new GatedWorkload();
        await using var child = CreateEnhancedChild(workload, out var handle);
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState { SourceReportsReleases = true };
        var codes = Enumerable.Range(0x4E00, TerminalWidgetHandle.MaxForwardedKeys + 1).ToArray();

        foreach (var code in codes[..^1])
        {
            await InputRouter.RouteInputAsync(node, StructuredPress(code), ring, state);
            Assert.AreEqual($"\x1b[{code}u", await workload.ReadInputAsync());
        }
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InputRouter.RouteInputAsync(node, StructuredPress(codes[^1]), ring, state));
        Assert.IsFalse(workload.Input.Reader.TryRead(out _), "Live keys are not synthetically released and the rejected press is not sent.");
        Assert.AreEqual(TerminalWidgetHandle.MaxForwardedKeys, handle.ForwardedKeyCount);

        await InputRouter.RouteInputAsync(node, StructuredRelease(codes[0]), ring, state);
        Assert.AreEqual($"\x1b[{codes[0]};1:3u", await workload.ReadInputAsync());
        await InputRouter.RouteInputAsync(node, StructuredPress(codes[^1]), ring, state);
        Assert.AreEqual($"\x1b[{codes[^1]}u", await workload.ReadInputAsync());
        foreach (var code in codes.Skip(1))
        {
            await InputRouter.RouteInputAsync(node, StructuredRelease(code), ring, state);
            Assert.AreEqual($"\x1b[{code};1:3u", await workload.ReadInputAsync());
        }
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.IsFalse(handle.HasAnyForwardedKeys);
    }

    [TestMethod]
    public async Task RouteInput_FirstHeldPressAfterSourceStartsReportingReleases_SurvivesStaleOverflow()
    {
        await using var workload = new GatedWorkload();
        await using var child = CreateEnhancedChild(workload, out var handle);
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var stale = Enumerable.Range(0x4E00, TerminalWidgetHandle.MaxForwardedKeys - 1).ToArray();
        foreach (var code in stale)
        {
            await InputRouter.RouteInputAsync(node, StructuredPress(code), ring, state);
            Assert.AreEqual($"\x1b[{code}u", await workload.ReadInputAsync());
        }

        // Negotiation confirms release reporting; the first held press is live ownership even
        // though no release has been observed yet.
        state.SourceReportsReleases = true;
        var held = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(node, held, ring, state);
        Assert.AreEqual("\x1b[97u", await workload.ReadInputAsync());

        foreach (var code in Enumerable.Range(0x6000, TerminalWidgetHandle.MaxForwardedKeys + 20))
        {
            await InputRouter.RouteInputAsync(node, StructuredPress(code), ring, state);
            Assert.AreEqual($"\x1b[{code}u", await workload.ReadInputAsync(), "Stale overflow is dropped without a release.");
            await InputRouter.RouteInputAsync(node, StructuredRelease(code), ring, state);
            Assert.AreEqual($"\x1b[{code};1:3u", await workload.ReadInputAsync());
        }
        Assert.IsTrue(handle.OwnsForwardedKey((Hex1bKey.A, null)));
        Assert.AreEqual(TerminalWidgetHandle.MaxForwardedKeys - 1, handle.ForwardedKeyCount,
            "Exactly one stale entry was dropped to admit the first transient press; later ones fit.");

        await InputRouter.RouteInputAsync(node, held with { Text = "", EventType = Hex1bKeyEventType.Release }, ring, state);
        Assert.AreEqual("\x1b[97;1:3u", await workload.ReadInputAsync());
        Assert.IsFalse(workload.Input.Reader.TryRead(out _), "The live press is released exactly once.");
    }

    [TestMethod]
    public async Task RouteInput_SourceStopsReportingReleases_DemotesHeldPressToBookkeeping()
    {
        await using var workload = new GatedWorkload();
        await using var child = CreateEnhancedChild(workload, out var handle);
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState { SourceReportsReleases = true };
        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 }, ring, state);
        Assert.AreEqual("\x1b[97u", await workload.ReadInputAsync());

        // The source can no longer promise a release, so the held press becomes bookkeeping.
        state.SourceReportsReleases = false;
        foreach (var code in Enumerable.Range(0x4E00, TerminalWidgetHandle.MaxForwardedKeys))
        {
            await InputRouter.RouteInputAsync(node, StructuredPress(code), ring, state);
            Assert.AreEqual($"\x1b[{code}u", await workload.ReadInputAsync());
        }
        Assert.IsFalse(handle.OwnsForwardedKey((Hex1bKey.A, null)), "The demoted press was the oldest stale entry.");
        Assert.IsFalse(workload.Input.Reader.TryRead(out _), "Dropping it did not synthesize a release.");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("\x1b[>1u")]
    public async Task RunAsync_RawLegacyCjkText_PastTrackingLimit_IsForwardedWithoutOwnership(string childMode)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var presentation = new RawBytePresentation();
        await using var childWorkload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(childWorkload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        child.ApplyTokens(AnsiTokenizer.Tokenize(childMode));
        Hex1bApp? app = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(_ => { }, instance =>
            {
                app = instance;
                return _ => new TerminalWidget(handle);
            })
            .WithPresentation(presentation).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            // Raw legacy bytes carry no key identity for non-ASCII text, so they never consume tracking slots.
            var text = Enumerable.Range(0x4E00, TerminalWidgetHandle.MaxForwardedKeys + 44)
                .Select(char.ConvertFromUtf32).ToArray();
            foreach (var character in text)
                presentation.Enqueue(Encoding.UTF8.GetBytes(character));
            foreach (var character in text)
                Assert.AreEqual(character, await childWorkload.ReadInputAsync());
            Assert.IsFalse(run.IsCompleted);
            Assert.AreEqual(0, handle.ForwardedKeyCount);

            app!.RequestStop();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "No releases are synthesized for untracked text.");
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
    }

    [TestMethod]
    public async Task RunAsync_StructuredSourceWithoutReleases_TypesPastTrackingLimitAndShutsDownCleanly()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var childWorkload = new GatedWorkload();
        await using var child = CreateEnhancedChild(childWorkload, out var handle);
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([new TextBlockWidget("Typing ready"), new TerminalWidget(handle)]);
                })
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Typing ready"), TimeSpan.FromSeconds(5), "typing app ready")
                .Build().ApplyAsync(host);
            Assert.IsNull(app!.InputSourceReportsKeyReleases, "Release capability is unknown until negotiated.");
            var codes = Enumerable.Range(0x4E00, TerminalWidgetHandle.MaxForwardedKeys + 44).ToArray();
            foreach (var code in codes)
            {
                Assert.IsTrue(appInput!.TryWriteInputEvent(StructuredPress(code)));
                Assert.AreEqual($"\x1b[{code}u", await childWorkload.ReadInputAsync());
            }
            Assert.IsFalse(run.IsCompleted, "Typing past the tracking limit must not fault app input.");
            Assert.AreEqual(TerminalWidgetHandle.MaxForwardedKeys, handle.ForwardedKeyCount);

            app.RequestStop();
            await run.WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var code in codes.Skip(44))
                Assert.AreEqual($"\x1b[{code};1:3u", await childWorkload.ReadInputAsync());
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
            Assert.IsFalse(handle.HasAnyForwardedKeys);
        }
        finally
        {
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) { }
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ReleaseForwardedKeys_WriteFailsOrCancels_KeepsOwnershipForRetry(bool cleanup, bool cancel)
    {
        await using var workload = new GatedWorkload
        {
            BlockAtWrite = 2,
            FailAtWrite = cancel ? 0 : 2,
            WriteFailure = cancel ? null : new IOException("release delivery failed")
        };
        await using var child = CreateEnhancedChild(workload, out var handle);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        state.ObserveKeyEvent(press);
        await handle.ForwardKeyAsync(press, state, CancellationToken.None);
        Assert.AreEqual("\x1b[97u", await workload.ReadInputAsync());
        var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };
        using var cancellation = new CancellationTokenSource();

        var delivery = cleanup
            ? state.ReleaseAllTerminalKeysAsync(cancellation.Token)
            : handle.ReleaseForwardedKeyAsync(release, cancellation.Token);
        try
        {
            await workload.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel)
            {
                cancellation.Cancel();
                if (cleanup)
                    await Assert.ThrowsAsync<TimeoutException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(5)));
                else
                    await Assert.ThrowsAsync<OperationCanceledException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(5)));
            }
            else
            {
                workload.Unblock.TrySetResult();
                var failure = await Assert.ThrowsAsync<IOException>(() => delivery.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreSame(workload.WriteFailure, failure);
            }
            Assert.IsTrue(handle.OwnsForwardedKey((Hex1bKey.A, null)));
            Assert.IsFalse(workload.Input.Reader.TryRead(out _));

            workload.Unblock.TrySetResult();
            await state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
            Assert.AreEqual("\x1b[97;1:3u", await workload.ReadInputAsync());
            Assert.IsFalse(handle.HasAnyForwardedKeys);
            Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        }
        finally
        {
            workload.Unblock.TrySetResult();
            cancellation.Cancel();
        }
    }

    [TestMethod]
    public async Task ReleaseForwardedKey_SecondRecipientFails_RetriesOnlyUndeliveredRecipient()
    {
        await using var oldWorkload = new GatedWorkload();
        await using var oldChild = CreateEnhancedChild(oldWorkload, out var handle);
        await using var newWorkload = new GatedWorkload { FailAtWrite = 2, WriteFailure = new IOException("second child") };
        await using var newChild = CreateEnhancedChild(newWorkload, out _);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        await handle.ForwardKeyAsync(press, state, CancellationToken.None);
        handle.Reset();
        ((ITerminalLifecycleAwarePresentationAdapter)handle).TerminalCreated(newChild);
        await handle.ForwardKeyAsync(press, state, CancellationToken.None);
        Assert.AreEqual("\x1b[97u", await oldWorkload.ReadInputAsync());
        Assert.AreEqual("\x1b[97u", await newWorkload.ReadInputAsync());

        var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };
        await Assert.ThrowsAsync<IOException>(() => handle.ReleaseForwardedKeyAsync(release, CancellationToken.None));
        Assert.AreEqual("\x1b[97;1:3u", await oldWorkload.ReadInputAsync());
        Assert.AreEqual(1, handle.ForwardedKeyCount);
        Assert.IsTrue(await handle.ReleaseForwardedKeyAsync(release, CancellationToken.None));
        Assert.AreEqual("\x1b[97;1:3u", await newWorkload.ReadInputAsync());
        Assert.IsFalse(handle.HasAnyForwardedKeys);
        Assert.IsFalse(oldWorkload.Input.Reader.TryRead(out _), "The successful recipient is not released twice.");
        Assert.IsFalse(newWorkload.Input.Reader.TryRead(out _));
    }

    [TestMethod]
    public async Task ForwardKey_RepeatedPressWriteFails_RetainsEarlierOwnership()
    {
        await using var workload = new GatedWorkload { FailAtWrite = 2, WriteFailure = new IOException("second press") };
        await using var child = CreateEnhancedChild(workload, out var handle);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        await handle.ForwardKeyAsync(press, state, CancellationToken.None);
        Assert.AreEqual("\x1b[97u", await workload.ReadInputAsync());
        await Assert.ThrowsAsync<IOException>(() => handle.ForwardKeyAsync(press, state, CancellationToken.None));

        Assert.IsTrue(handle.OwnsForwardedKey((Hex1bKey.A, null)));
        await state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
        Assert.AreEqual("\x1b[97;1:3u", await workload.ReadInputAsync());
        Assert.IsFalse(handle.HasAnyForwardedKeys);
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RouteInput_PhysicalReleaseRacesCleanup_DeliversExactlyOneRelease(bool cleanupFirst)
    {
        await using var workload = new GatedWorkload { BlockAtWrite = 2 };
        await using var child = CreateEnhancedChild(workload, out var handle);
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(node, press, ring, state);
        Assert.AreEqual("\x1b[97u", await workload.ReadInputAsync());
        var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };

        Task first = cleanupFirst
            ? state.ReleaseAllTerminalKeysAsync(CancellationToken.None)
            : InputRouter.RouteInputAsync(node, release, ring, state);
        try
        {
            await workload.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Task second = cleanupFirst
                ? InputRouter.RouteInputAsync(node, release, ring, state)
                : state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
            Assert.IsFalse(second.IsCompleted, "The competing release waits for the in-flight delivery.");
            Assert.IsTrue(handle.HasAnyForwardedKeys);
            workload.Unblock.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual("\x1b[97;1:3u", await workload.ReadInputAsync());
            Assert.IsFalse(workload.Input.Reader.TryRead(out _));
            Assert.IsFalse(handle.HasAnyForwardedKeys);
        }
        finally
        {
            workload.Unblock.TrySetResult();
        }
    }

    [TestMethod]
    [DataRow(57441, Hex1bModifiers.Shift)]
    [DataRow(57447, Hex1bModifiers.Shift)]
    [DataRow(57442, Hex1bModifiers.Control)]
    [DataRow(57448, Hex1bModifiers.Control)]
    [DataRow(57443, Hex1bModifiers.Alt)]
    [DataRow(57449, Hex1bModifiers.Alt)]
    [DataRow(57444, Hex1bModifiers.Super)]
    [DataRow(57450, Hex1bModifiers.Super)]
    [DataRow(57445, Hex1bModifiers.Hyper)]
    [DataRow(57451, Hex1bModifiers.Hyper)]
    [DataRow(57446, Hex1bModifiers.Meta)]
    [DataRow(57452, Hex1bModifiers.Meta)]
    public async Task ReleaseAllTerminalKeys_ModifierKey_ReportsPostReleaseStateAndPreservesLocks(int code, Hex1bModifiers modifier)
    {
        await using var workload = new GatedWorkload();
        await using var child = CreateEnhancedChild(workload, out var handle);
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();
        var press = new Hex1bKeyEvent(Hex1bKey.None, "", modifier | Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock)
        {
            UnicodeKeyCode = code
        };
        await InputRouter.RouteInputAsync(node, press, ring, state);
        await workload.ReadInputAsync();
        await InputRouter.RouteInputAsync(node,
            new Hex1bKeyEvent(Hex1bKey.A, "a", press.Modifiers) { UnicodeKeyCode = 97 }, ring, state);
        await workload.ReadInputAsync();

        await state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
        Assert.AreEqual($"\x1b[{code};193:3u", await workload.ReadInputAsync());
        Assert.AreEqual("\x1b[97;193:3u", await workload.ReadInputAsync(), "Subsequent releases use the updated modifier state.");
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(5)]
    public async Task ReleaseAllTerminalKeys_ModifierReleaseFails_RetryRetainsPostDeliveryState(int failAtWrite)
    {
        await using var workload = new GatedWorkload { FailAtWrite = failAtWrite, WriteFailure = new IOException("modifier release") };
        await using var child = CreateEnhancedChild(workload, out var handle);
        var state = new InputRouterState();
        const Hex1bModifiers modifiers = Hex1bModifiers.Control | Hex1bModifiers.NumLock;
        foreach (var code in new[] { 57442, 57448, 97 })
        {
            var press = new Hex1bKeyEvent(Hex1bKey.None, "", modifiers) { UnicodeKeyCode = code };
            state.ObserveKeyEvent(press);
            await handle.ForwardKeyAsync(press, state, CancellationToken.None);
            await workload.ReadInputAsync();
        }

        await Assert.ThrowsAsync<IOException>(() => state.ReleaseAllTerminalKeysAsync(CancellationToken.None));
        if (failAtWrite == 5)
        {
            Assert.AreEqual("\x1b[57442;133:3u", await workload.ReadInputAsync());
            Assert.AreEqual(2, handle.ForwardedKeyCount);
        }
        else
            Assert.AreEqual(3, handle.ForwardedKeyCount);
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));

        await state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
        if (failAtWrite == 4)
            Assert.AreEqual("\x1b[57442;133:3u", await workload.ReadInputAsync());
        Assert.AreEqual("\x1b[57448;129:3u", await workload.ReadInputAsync());
        Assert.AreEqual("\x1b[97;129:3u", await workload.ReadInputAsync());
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.IsFalse(handle.HasAnyForwardedKeys);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ReleaseAllTerminalKeys_BothControlsAndLockKeys_PreservesOtherControlUntilItsRelease(bool separateHandles)
    {
        await using var workload = new GatedWorkload();
        await using var child = CreateEnhancedChild(workload, out var handle);
        await using var otherWorkload = new GatedWorkload();
        await using var otherChild = CreateEnhancedChild(otherWorkload, out var otherHandle);
        var state = new InputRouterState();
        const Hex1bModifiers modifiers = Hex1bModifiers.Control | Hex1bModifiers.CapsLock | Hex1bModifiers.NumLock;
        var left = new Hex1bKeyEvent(Hex1bKey.None, "", modifiers) { UnicodeKeyCode = 57442 };
        var right = left with { UnicodeKeyCode = 57448 };
        state.ObserveKeyEvent(left);
        await handle.ForwardKeyAsync(left, state, CancellationToken.None);
        state.ObserveKeyEvent(right);
        await (separateHandles ? otherHandle : handle).ForwardKeyAsync(right, state, CancellationToken.None);
        await workload.ReadInputAsync();
        await (separateHandles ? otherWorkload : workload).ReadInputAsync();
        foreach (var code in new[] { 57358, 57360, 97 })
        {
            var press = new Hex1bKeyEvent(Hex1bKey.None, "", modifiers) { UnicodeKeyCode = code };
            state.ObserveKeyEvent(press);
            await (separateHandles ? otherHandle : handle).ForwardKeyAsync(press, state, CancellationToken.None);
            await (separateHandles ? otherWorkload : workload).ReadInputAsync();
        }

        await state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
        Assert.AreEqual("\x1b[57442;197:3u", await workload.ReadInputAsync(), "Right Control is still held.");
        var remaining = separateHandles ? otherWorkload : workload;
        Assert.AreEqual("\x1b[57448;193:3u", await remaining.ReadInputAsync(), "Neither Control is now held.");
        Assert.AreEqual("\x1b[57358;193:3u", await remaining.ReadInputAsync(), "Releasing Caps Lock does not toggle its lock state.");
        Assert.AreEqual("\x1b[57360;193:3u", await remaining.ReadInputAsync(), "Releasing Num Lock does not toggle its lock state.");
        Assert.AreEqual("\x1b[97;193:3u", await remaining.ReadInputAsync());
        Assert.IsFalse(workload.Input.Reader.TryRead(out _));
        Assert.IsFalse(otherWorkload.Input.Reader.TryRead(out _));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RouteInput_HandleResetAndReattach_ReleasesOnlyTheChildThatReceivedThePress(bool oldChildRunning)
    {
        await using var oldWorkload = new GatedWorkload();
        var oldChild = CreateEnhancedChild(oldWorkload, out _);
        await using var newWorkload = new GatedWorkload();
        await using var newChild = CreateEnhancedChild(newWorkload, out _);
        await using var handle = new TerminalWidgetHandle(24, 4);
        var lifecycle = (ITerminalLifecycleAwarePresentationAdapter)handle;
        lifecycle.TerminalCreated(oldChild);
        var node = new TerminalNode { Handle = handle };
        var root = new RecordingContainerNode(node);
        var ring = new FocusRing();
        ring.Rebuild(root);
        ring.Focus(node);
        var state = new InputRouterState();
        try
        {
            var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
            await InputRouter.RouteInputAsync(root, press, ring, state);
            Assert.AreEqual("\x1b[97u", await oldWorkload.ReadInputAsync());

            handle.Reset();
            lifecycle.TerminalCreated(newChild);
            if (!oldChildRunning)
                await oldChild.DisposeAsync();

            Assert.AreEqual(InputResult.Handled, await InputRouter.RouteInputAsync(
                root, press with { EventType = Hex1bKeyEventType.Repeat }, ring, state),
                "The reattached child never received the press, so it gets no repeat.");
            var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };
            Assert.AreEqual(InputResult.Handled, await InputRouter.RouteInputAsync(root, release, ring, state));
            if (oldChildRunning)
                Assert.AreEqual("\x1b[97;1:3u", await oldWorkload.ReadInputAsync());

            Assert.AreEqual(InputResult.NotHandled, await InputRouter.RouteInputAsync(root, release, ring, state));
            Assert.IsFalse(oldWorkload.Input.Reader.TryRead(out _), "The original child is released exactly once.");
            Assert.IsFalse(newWorkload.Input.Reader.TryRead(out _), "The reattached child never saw the press.");
            Assert.AreSame(release, TestSeq.Single(root.ReceivedInputs), "Only the unmatched duplicate bubbles.");
            Assert.IsFalse(handle.HasAnyForwardedKeys);
        }
        finally
        {
            await oldChild.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task RouteInput_UnattachedHandle_TracksNoPressAndReleaseBubbles()
    {
        await using var workload = new GatedWorkload();
        await using var child = CreateEnhancedChild(workload, out _);
        await using var handle = new TerminalWidgetHandle(24, 4);
        var node = new TerminalNode { Handle = handle };
        var root = new RecordingContainerNode(node);
        var ring = new FocusRing();
        ring.Rebuild(root);
        ring.Focus(node);
        var state = new InputRouterState();

        var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
        await InputRouter.RouteInputAsync(root, press, ring, state);
        Assert.IsFalse(handle.HasAnyForwardedKeys, "No child received the press.");

        ((ITerminalLifecycleAwarePresentationAdapter)handle).TerminalCreated(child);
        var release = press with { Text = "", EventType = Hex1bKeyEventType.Release };
        Assert.AreEqual(InputResult.NotHandled, await InputRouter.RouteInputAsync(root, release, ring, state));
        Assert.IsFalse(workload.Input.Reader.TryRead(out _), "A later-attached child must not get an unmatched release.");
        Assert.AreSame(release, TestSeq.Single(root.ReceivedInputs));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task ReleaseAllTerminalKeys_NoPriorRelease_UsesLastKnownLockStateAndLegacyEncoding(bool childEnhanced)
    {
        await using var workload = new GatedWorkload();
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        if (childEnhanced) child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        var node = new TerminalNode { Handle = handle, IsFocused = true };
        var ring = new FocusRing();
        ring.Rebuild(node);
        var state = new InputRouterState();

        await InputRouter.RouteInputAsync(node, Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 }, ring, state);
        // Caps Lock engaged after the first press; cleanup reports the latest known state.
        await InputRouter.RouteInputAsync(node,
            new Hex1bKeyEvent(Hex1bKey.B, "B", Hex1bModifiers.CapsLock) { UnicodeKeyCode = 98 }, ring, state);
        Assert.AreEqual(childEnhanced ? "\x1b[97u" : "a", await workload.ReadInputAsync());
        Assert.AreEqual(childEnhanced ? "\x1b[98;65u" : "B", await workload.ReadInputAsync());

        await state.ReleaseAllTerminalKeysAsync(CancellationToken.None);
        if (childEnhanced)
        {
            Assert.AreEqual("\x1b[97;65:3u", await workload.ReadInputAsync());
            Assert.AreEqual("\x1b[98;65:3u", await workload.ReadInputAsync());
        }
        Assert.IsFalse(workload.Input.Reader.TryRead(out _), "Legacy children have no release encoding.");
        Assert.IsFalse(handle.HasAnyForwardedKeys);
    }

    [TestMethod]
    public async Task RunAsync_EmbeddedReleaseCleanupTimesOut_SurfacesFailureAndKeepsOwnership()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        // Write 1 is the press; write 2 is the shutdown release, which never completes.
        await using var childWorkload = new GatedWorkload { BlockAtWrite = 2 };
        await using var child = CreateEnhancedChild(childWorkload, out var handle);
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                instance =>
                {
                    app = instance;
                    instance.EmbeddedKeyReleaseTimeout = TimeSpan.FromMilliseconds(50);
                    return _ => new VStackWidget([new TextBlockWidget("Timeout ready"), new TerminalWidget(handle)]);
                })
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Timeout ready"), TimeSpan.FromSeconds(5), "cleanup timeout app ready")
                .Build().ApplyAsync(host);
            appInput!.TryWriteInputEvent(Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 });
            Assert.AreEqual("\x1b[97u", await childWorkload.ReadInputAsync());

            app!.RequestStop();
            await childWorkload.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(handle.OwnsForwardedKey((Hex1bKey.A, null)), "An undelivered release keeps its ownership.");
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _));
        }
        finally
        {
            childWorkload.Unblock.TrySetResult();
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException) { }
        }
    }

    [TestMethod]
    [DataRow("stop")]
    [DataRow("cancel")]
    [DataRow("dispose")]
    [DataRow("released-first")]
    [DataRow("no-prior-release")]
    public async Task RunAsync_ShutdownWithHeldEmbeddedKey_ReleasesExternallyOwnedChildOnce(string scenario)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await using var childWorkload = new GatedWorkload();
        await using var child = CreateEnhancedChild(childWorkload, out var handle);
        Hex1bApp? app = null;
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                instance =>
                {
                    app = instance;
                    return _ => new VStackWidget([new TextBlockWidget("Shutdown ready"), new TerminalWidget(handle)]);
                })
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Shutdown ready"), TimeSpan.FromSeconds(5), "embedded shutdown app ready")
                .Build().ApplyAsync(host);
            var other = Hex1bKeyEvent.Plain(Hex1bKey.X, 'x') with { UnicodeKeyCode = 120 };
            if (scenario != "no-prior-release")
            {
                // A delivered release shows the host reports event types; the no-prior-release
                // scenario covers an enhanced first press or a source that never reports releases.
                appInput!.TryWriteInputEvent(other);
                appInput.TryWriteInputEvent(other with { Text = "", EventType = Hex1bKeyEventType.Release });
                Assert.AreEqual("\x1b[120u", await childWorkload.ReadInputAsync());
                Assert.AreEqual("\x1b[120;1:3u", await childWorkload.ReadInputAsync());
            }
            var press = Hex1bKeyEvent.Plain(Hex1bKey.A, 'a') with { UnicodeKeyCode = 97 };
            appInput!.TryWriteInputEvent(press);
            Assert.AreEqual("\x1b[97u", await childWorkload.ReadInputAsync());
            if (scenario == "released-first")
            {
                appInput.TryWriteInputEvent(press with { Text = "", EventType = Hex1bKeyEventType.Release });
                Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
            }

            switch (scenario)
            {
                case "cancel": cancellation.Cancel(); break;
                case "dispose": await app!.DisposeAsync(); break;
                default: app!.RequestStop(); break;
            }
            await run.WaitAsync(TimeSpan.FromSeconds(5));

            // A tracked press proves the child received it, so cleanup neutralizes it even when the
            // host has not yet reported any release.
            if (scenario != "released-first")
                Assert.AreEqual("\x1b[97;1:3u", await childWorkload.ReadInputAsync());
            Assert.IsFalse(childWorkload.Input.Reader.TryRead(out _), "Shutdown must not duplicate or invent releases.");
            Assert.IsFalse(handle.HasAnyForwardedKeys);
            Assert.AreNotEqual(TerminalState.Completed, handle.State, "The externally owned child outlives the app.");
        }
        finally
        {
            cancellation.Cancel();
            await run.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task RunAsync_ChildChannelClosedAfterAppInputCompletes_PropagatesChildFailure(bool childFails)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var expected = new ChannelClosedException();
        await using var workload = new GatedWorkload { BlockFirstWrite = true, WriteFailure = childFails ? expected : null };
        await using var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out var handle).Build();
        Hex1bAppWorkloadAdapter? appInput = null;
        await using var host = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(
                options => appInput = TestSeq.IsType<Hex1bAppWorkloadAdapter>(options.WorkloadAdapter),
                _ => new VStackWidget([new TextBlockWidget("Closed race ready"), new TerminalWidget(handle)]))
            .WithHeadless().WithDimensions(30, 8).Build();
        var run = host.RunAsync(cancellation.Token);
        try
        {
            using var ready = await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.ContainsText("Closed race ready"), TimeSpan.FromSeconds(5), "closed race app ready")
                .Build().ApplyAsync(host);
            if (childFails)
            {
                appInput!.SendKey(Hex1bKey.A, 'a');
                await workload.FirstWrite.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            // The final app event is already dequeued; app input now completes with nothing left to drain.
            await appInput!.DisposeAsync();
            Assert.IsTrue(appInput.InputEvents.Completion.IsCompleted);
            workload.Unblock.TrySetResult();

            if (childFails)
            {
                var error = await Assert.ThrowsAsync<ChannelClosedException>(() => run.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreSame(expected, error);
            }
            else
            {
                await run.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        finally
        {
            workload.Unblock.TrySetResult();
            cancellation.Cancel();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (ChannelClosedException ex) when (ReferenceEquals(ex, expected)) { }
        }
    }

    private static Hex1bTerminal CreateEnhancedChild(GatedWorkload workload, out TerminalWidgetHandle handle)
    {
        var child = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload).WithDimensions(24, 4).WithTerminalWidget(out handle).Build();
        child.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[>11u"));
        return child;
    }

    private sealed class RecordingContainerNode : Hex1bNode
    {
        public RecordingContainerNode(params Hex1bNode[] children)
        {
            foreach (var child in children)
            {
                Children.Add(child);
                child.Parent = this;
            }
        }

        public List<Hex1bNode> Children { get; } = [];
        public List<Hex1bEvent> ReceivedInputs { get; } = [];
        public Action<InputBindingsBuilder>? BindingsConfig { get; set; }

        public override InputResult HandleInput(Hex1bEvent inputEvent)
        {
            ReceivedInputs.Add(inputEvent);
            return InputResult.NotHandled;
        }

        public override void ConfigureDefaultBindings(InputBindingsBuilder bindings) => BindingsConfig?.Invoke(bindings);
        protected override Layout.Size MeasureCore(Layout.Constraints constraints) => new(80, 24);
        public override void Render(Hex1bRenderContext context) { }
        public override IEnumerable<Hex1bNode> GetChildren() => Children;
        public override IEnumerable<Hex1bNode> GetFocusableNodes() => Children.SelectMany(c => c.GetFocusableNodes());
    }

    private sealed class RawBytePresentation : IHex1bTerminalPresentationAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        public int Width => 40;
        public int Height => 8;
        public TerminalCapabilities Capabilities => new();
        public event Action<int, int>? Resized { add { } remove { } }
        public event Action? Disconnected { add { } remove { } }
        public void Enqueue(byte[] bytes) => Assert.IsTrue(_input.Writer.TryWrite(bytes));
        public async ValueTask<ReadOnlyMemory<byte>> ReadInputAsync(CancellationToken ct = default)
            => await _input.Reader.ReadAsync(ct);
        public ValueTask WriteOutputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask FlushAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask EnterRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask ExitRawModeAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
        public (int Row, int Column) GetCursorPosition() => (0, 0);
        public ValueTask DisposeAsync()
        {
            _input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class GatedWorkload : IHex1bTerminalWorkloadAdapter
    {
        private readonly Channel<ReadOnlyMemory<byte>> _output = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        internal Channel<string> Input { get; } = Channel.CreateUnbounded<string>();
        internal TaskCompletionSource FirstWrite { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Unblock { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool BlockFirstWrite { get; init; }
        /// <summary>One-based write number that blocks until <see cref="Unblock"/> completes.</summary>
        internal int BlockAtWrite { get; init; }
        internal int FailAtWrite { get; init; }
        internal Exception? WriteFailure { get; init; }
        private int _writes;

        internal async Task<string> ReadInputAsync()
            => await Input.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public async ValueTask<ReadOnlyMemory<byte>> ReadOutputAsync(CancellationToken ct = default)
            => await _output.Reader.ReadAsync(ct);

        public async ValueTask WriteInputAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            var number = Interlocked.Increment(ref _writes);
            var first = number == 1;
            if (first) FirstWrite.TrySetResult();
            if ((first && BlockFirstWrite) || number == BlockAtWrite)
            {
                Blocked.TrySetResult();
                await Unblock.Task.WaitAsync(ct);
            }
            if (WriteFailure is not null && (FailAtWrite == 0 || number == FailAtWrite)) throw WriteFailure;
            await Input.Writer.WriteAsync(Encoding.UTF8.GetString(data.Span), ct);
        }

        public ValueTask ResizeAsync(int width, int height, CancellationToken ct = default) => ValueTask.CompletedTask;
        public event Action? Disconnected { add { } remove { } }
        public ValueTask DisposeAsync()
        {
            Unblock.TrySetResult();
            _output.Writer.TryComplete();
            Input.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
