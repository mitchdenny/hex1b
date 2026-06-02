using System.Text;
using Hex1b.Input;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Tests for bracketed paste streaming through the terminal dispatch pipeline.
/// These tests verify that SpecialKeyToken(200/201) markers are correctly detected
/// and paste data is streamed through a PasteContext.
/// </summary>
[TestClass]
public class PasteStreamingTests
{
    /// <summary>
    /// Helper to dispatch tokens through the terminal and collect resulting events.
    /// Uses reflection to call the private DispatchTokensAsEventsAsync method.
    /// </summary>
    private static async Task<List<Hex1bEvent>> DispatchTokensAsync(IReadOnlyList<AnsiToken> tokens)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();

        // Use reflection to call the private method
        var method = typeof(Hex1bTerminal).GetMethod(
            "DispatchTokensAsEventsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        await (Task)method!.Invoke(terminal, [tokens, workload, CancellationToken.None])!;

        // Read all events from the channel
        var events = new List<Hex1bEvent>();
        while (workload.InputEvents.TryRead(out var evt))
        {
            events.Add(evt);
        }
        return events;
    }

    [TestMethod]
    public async Task SmallPaste_EmitsPasteEvent()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200), // Paste start
            new TextToken("hello"),
            new SpecialKeyToken(201), // Paste end
        };

        var events = await DispatchTokensAsync(tokens);

        var pasteEvent = TestSeq.Single(events);
        var paste = TestSeq.IsType<Hex1bPasteEvent>(pasteEvent);
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("hello", text);
    }

    [TestMethod]
    public async Task EmptyPaste_EmitsPasteEventWithEmptyText()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var pasteEvent = TestSeq.Single(events);
        var paste = TestSeq.IsType<Hex1bPasteEvent>(pasteEvent);
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("", text);
    }

    [TestMethod]
    public async Task SingleCharPaste_EmitsPasteEvent()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("x"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("x", text);
    }

    [TestMethod]
    public async Task MultiLinePaste_PreservesNewlines()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("line1"),
            new ControlCharacterToken('\n'),
            new TextToken("line2"),
            new ControlCharacterToken('\r'),
            new ControlCharacterToken('\n'),
            new TextToken("line3"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("line1\nline2\r\nline3", text);
    }

    [TestMethod]
    public async Task PasteWithTabs_PreservesTabs()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("col1"),
            new ControlCharacterToken('\t'),
            new TextToken("col2"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("col1\tcol2", text);
    }

    [TestMethod]
    public async Task NonPasteInputDuringPaste_DispatchedNormally()
    {
        // Mouse event during paste should still be dispatched as normal
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("paste data"),
            new SgrMouseToken(MouseButton.Left, MouseAction.Down, 5, 10, Input.Hex1bModifiers.None, 0),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        // Should have both the paste event and the mouse event
        Assert.AreEqual(2, events.Count);
        TestSeq.IsType<Hex1bPasteEvent>(events[0]);
        TestSeq.IsType<Hex1bMouseEvent>(events[1]);

        // Paste should have the text data only
        var paste = (Hex1bPasteEvent)events[0];
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("paste data", text);
    }

    [TestMethod]
    public async Task NormalInput_NotAffectedByPasteSupport()
    {
        // Regular text input (not bracketed) should still work as before
        var tokens = new AnsiToken[]
        {
            new TextToken("hello"),
            new ControlCharacterToken('\r'), // Enter
        };

        var events = await DispatchTokensAsync(tokens);

        Assert.AreEqual(2, events.Count);
        TestSeq.All(events, e => TestSeq.IsType<Hex1bKeyEvent>(e));
    }

    [TestMethod]
    public async Task MultipleChunks_StreamedCorrectly()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("chunk1"),
            new TextToken("chunk2"),
            new TextToken("chunk3"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("chunk1chunk2chunk3", text);
    }

    [TestMethod]
    public async Task PasteFollowedByNormalInput()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("pasted"),
            new SpecialKeyToken(201),
            new TextToken("typed"),
        };

        var events = await DispatchTokensAsync(tokens);

        Assert.AreEqual(2, events.Count);
        var paste = TestSeq.IsType<Hex1bPasteEvent>(events[0]);
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("pasted", text);

        var keyEvent = TestSeq.IsType<Hex1bKeyEvent>(events[1]);
        Assert.AreEqual("typed", keyEvent.Text);
    }

    [TestMethod]
    public async Task NormalInputFollowedByPaste()
    {
        var tokens = new AnsiToken[]
        {
            new TextToken("typed"),
            new SpecialKeyToken(200),
            new TextToken("pasted"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        Assert.AreEqual(2, events.Count);
        TestSeq.IsType<Hex1bKeyEvent>(events[0]);
        TestSeq.IsType<Hex1bPasteEvent>(events[1]);
    }

    [TestMethod]
    public async Task TwoConsecutivePastes_TwoSeparateEvents()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("first"),
            new SpecialKeyToken(201),
            new SpecialKeyToken(200),
            new TextToken("second"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        Assert.AreEqual(2, events.Count);
        var paste1 = TestSeq.IsType<Hex1bPasteEvent>(events[0]);
        var paste2 = TestSeq.IsType<Hex1bPasteEvent>(events[1]);

        Assert.AreEqual("first", await paste1.Paste.ReadToEndAsync());
        Assert.AreEqual("second", await paste2.Paste.ReadToEndAsync());
    }

    [TestMethod]
    public async Task LargePaste_StreamsCorrectly()
    {
        // 1MB paste — should stream without issues
        var largeText = new string('x', 1024 * 1024);
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken(largeText),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        var text = await paste.Paste.ReadToEndAsync(maxCharacters: 2 * 1024 * 1024);
        Assert.AreEqual(1024 * 1024, text.Length);
    }

    [TestMethod]
    public async Task PasteContext_IsCompleted_AfterEndMarker()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("data"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        Assert.IsTrue(paste.Paste.IsCompleted);
        await paste.Paste.Completed; // Should not hang
    }

    [TestMethod]
    public void EnterTuiMode_EnablesBracketedPaste_WhenSupported()
    {
        var caps = new TerminalCapabilities
        {
            SupportsMouse = true,
            SupportsBracketedPaste = true,
        };
        using var workload = new Hex1bAppWorkloadAdapter(caps);

        workload.EnterTuiMode();

        // Read the output to verify the bracketed paste enable sequence was written
        Assert.IsTrue(workload.Capabilities.SupportsBracketedPaste);

        // Drain the output channel and check for the mode 2004 sequence
        var output = DrainOutputText(workload);
        Assert.Contains("\x1b[?2004h", output);
    }

    [TestMethod]
    public void ExitTuiMode_DisablesBracketedPaste_WhenSupported()
    {
        var caps = new TerminalCapabilities
        {
            SupportsMouse = true,
            SupportsBracketedPaste = true,
        };
        using var workload = new Hex1bAppWorkloadAdapter(caps);

        workload.EnterTuiMode();
        DrainOutputText(workload); // consume enter output
        workload.ExitTuiMode();

        var output = DrainOutputText(workload);
        Assert.Contains("\x1b[?2004l", output);
    }

    [TestMethod]
    public void EnterTuiMode_NoBracketedPaste_WhenNotSupported()
    {
        var caps = new TerminalCapabilities
        {
            SupportsMouse = true,
            SupportsBracketedPaste = false,
        };
        using var workload = new Hex1bAppWorkloadAdapter(caps);

        workload.EnterTuiMode();

        var output = DrainOutputText(workload);
        Assert.DoesNotContain("\x1b[?2004h", output);
    }

    private static string DrainOutputText(Hex1bAppWorkloadAdapter workload)
    {
        var sb = new StringBuilder();
        while (workload.TryReadOutput(out var data))
        {
            sb.Append(Encoding.UTF8.GetString(data.Span));
        }
        return sb.ToString();
    }

    [TestMethod]
    public async Task UnicodeInPaste_PreservedCorrectly()
    {
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("こんにちは 🌍 café"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        var text = await paste.Paste.ReadToEndAsync();
        Assert.AreEqual("こんにちは 🌍 café", text);
    }

    [TestMethod]
    public async Task PasteReadLines_AcrossTokenBoundaries()
    {
        // Line break split across two TextTokens
        var tokens = new AnsiToken[]
        {
            new SpecialKeyToken(200),
            new TextToken("hello wor"),
            new TextToken("ld\nbye"),
            new SpecialKeyToken(201),
        };

        var events = await DispatchTokensAsync(tokens);

        var paste = TestSeq.IsType<Hex1bPasteEvent>(TestSeq.Single(events));
        var lines = new List<string>();
        await foreach (var line in paste.Paste.ReadLinesAsync())
        {
            lines.Add(line);
        }

        Assert.AreEqual(2, lines.Count);
        Assert.AreEqual("hello world", lines[0]);
        Assert.AreEqual("bye", lines[1]);
    }
}
