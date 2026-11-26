using System.Diagnostics;
using System.Text;
using System.Threading.Channels;
using Hex1b.Sixel;
using Hex1b.Tokens;

namespace Hex1b.Tests;

/// <summary>
/// Kitty keyboard support detection at the console boundary. The specification's
/// detection recipe is: send <c>CSI ? u</c> followed by DA1; a <c>CSI ? flags u</c>
/// reply means support, and DA1 arriving first means no support. Under ConPTY the
/// console host answers DA1 itself before the outer terminal's Kitty reply, so the
/// Windows driver waits for the deadline instead of treating DA1 as conclusive.
/// </summary>
[TestClass]
public class KittyConsoleProbeTests
{
    private static readonly TimeSpan ShortProbe = TimeSpan.FromMilliseconds(40);

    private static async Task<(ConsolePresentationAdapter Adapter, string Input)> ProbeAsync(
        FakeConsoleDriver driver, bool kittyDa1Conclusive = true)
    {
        var adapter = new ConsolePresentationAdapter(
            driver, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: kittyDa1Conclusive);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        return (adapter, await DrainPrefetchedAsync(adapter));
    }

    private static async Task<string> DrainPrefetchedAsync(ConsolePresentationAdapter adapter)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var bytes = await adapter.ReadInputAsync(cancellation.Token);
        return Encoding.ASCII.GetString(bytes.Span);
    }

    private static string[] Bytewise(string text) => text.Select(c => c.ToString()).ToArray();

    [TestMethod]
    public async Task EnterRawModeAsync_WritesKittyQueryBeforeDa1()
    {
        using var driver = new FakeConsoleDriver();
        var (adapter, _) = await ProbeAsync(driver);
        await adapter.DisposeAsync();

        var written = driver.WrittenText;
        var kitty = written.IndexOf("\x1b[?u", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, kitty);
        Assert.IsGreaterThan(kitty, written.IndexOf("\x1b[c", StringComparison.Ordinal));
        Assert.DoesNotContain("\x1b[>", written, "Support detection must never push keyboard flags.");
        Assert.DoesNotContain("\x1b[<u", written);
    }

    // Any well-formed reply means the protocol is supported, including flags 0 (nothing
    // currently enabled) and reserved bits: the effective flags are negotiated after push.
    [TestMethod]
    [DataRow("\x1b[?0u")]
    [DataRow("\x1b[?1u")]
    [DataRow("\x1b[?31u")]
    [DataRow("\x1b[?0031u")]
    [DataRow("\x1b[?32u")]
    [DataRow("\x1b[?255u")]
    public async Task EnterRawModeAsync_ValidKittyReply_EnablesSupportAndConsumesReply(string reply)
    {
        using var driver = new FakeConsoleDriver($"a{reply}b\x1b[?62;22cc");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("abc", input);
    }

    // Malformed or nonmatching replies are not probe replies: they stay input byte-for-byte.
    [TestMethod]
    [DataRow("\x1b[?;1u")]
    [DataRow("\x1b[?1;2u")]
    [DataRow("\x1b[?1:2u")]
    [DataRow("\x1b[?u")]
    [DataRow("\x1b[?1234567890u")]
    [DataRow("\x1b[?99999999999999999999u")]
    [DataRow("\x1b[?1 u")]
    public async Task EnterRawModeAsync_MalformedKittyReply_IsPreservedWithoutEnablingSupport(string reply)
    {
        using var driver = new FakeConsoleDriver($"a{reply}b");
        var (adapter, input) = await ProbeAsync(driver, kittyDa1Conclusive: false);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual($"a{reply}b", input);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnterRawModeAsync_MalformedThenValidReply_PreservesMalformedAndEnablesSupport(bool bytewise)
    {
        const string replies = "a\x1b[?1;2ub\x1b[?;uc\x1b[?3ud\x1b[?62c";
        using var driver = bytewise
            ? new FakeConsoleDriver(Bytewise(replies))
            : new FakeConsoleDriver("a\x1b[?1;2ub\x1b[?;uc", "\x1b[?3ud", "\x1b[?62c");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("a\x1b[?1;2ub\x1b[?;ucd", input);
    }

    [TestMethod]
    [DataRow(false, true, true, DisplayName = "Kitty then DA1, spec-conclusive DA1")]
    [DataRow(false, false, true, DisplayName = "Kitty then DA1, ConPTY")]
    [DataRow(true, true, false, DisplayName = "DA1 then Kitty, spec-conclusive DA1")]
    [DataRow(true, false, true, DisplayName = "DA1 then Kitty, ConPTY")]
    public async Task EnterRawModeAsync_ReplyOrder_FollowsDa1Conclusiveness(
        bool da1First, bool da1Conclusive, bool expected)
    {
        const string kitty = "\x1b[?1u";
        const string da1 = "\x1b[?61;6;7;21;22;23;24;28;32;42c";
        using var driver = new FakeConsoleDriver(da1First ? [da1, kitty, "x"] : [kitty, da1, "x"]);
        var (adapter, input) = await ProbeAsync(driver, da1Conclusive);
        await using var _ = adapter;

        Assert.AreEqual(expected, adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("x", input, "Both replies belong to the probe and must not reach the app.");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task EnterRawModeAsync_ByteByByteReplies_AreReassembled(bool da1First)
    {
        var replies = da1First ? "a\x1b[?62;4cb\x1b[?15uc" : "a\x1b[?15ub\x1b[?62;4cc";
        using var driver = new FakeConsoleDriver(Bytewise(replies));
        var (adapter, input) = await ProbeAsync(driver, kittyDa1Conclusive: false);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("abc", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_AdjacentUnrelatedInput_IsPreservedInOrder()
    {
        const string before = "x\x1b[A\x1b[<0;3;4M\x1bOP\x1b[97;5u";
        const string after = "\x1b[13~\x1b[1;1:3A\x1b[57441;2u\x1b[?1;2$yy";
        using var driver = new FakeConsoleDriver(before + "\x1b[?31u" + after);
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(before + after, input);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnterRawModeAsync_EscapeKeyBeforeReply_DetectsReplyAndPreservesEscape(bool separateReads)
    {
        using var driver = separateReads
            ? new FakeConsoleDriver("\x1b", "\x1b[?1u", "z")
            : new FakeConsoleDriver("\x1b\x1b[?1uz");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("\x1bz", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_EscapeKeyBeforePaste_KeepsPastedReplyAsInput()
    {
        const string input = "\x1b\x1b[200~\x1b[?1u\x1b[201~";
        using var driver = new FakeConsoleDriver(input);
        var (adapter, received) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(input, received);
    }
    public static IEnumerable<object[]> FramedReplies =>
    [
        ["\x1b[200~\x1b[?1u\x1b[?62;4c\x1b[201~"],
        ["\x1b]52;c;\x1b[?1u\x1b[?62c\x07"],
        ["\x1b]0;title \x1b[?1u\x1b[?62c\x1b\\"],
        ["\x1b_Gi=7;\x1b[?1u\x1b[?62c\x1b\\"],
        ["\x1bP1$r\x1b[?1u\x1b[?62c\x1b\\"],
        ["\x1bXsos \x1b[?1u\x1b\\\x1b^pm \x1b[?1u\x1b\\"],
    ];

    [TestMethod]
    [DynamicData(nameof(FramedReplies))]
    public async Task EnterRawModeAsync_ReplyInsideFrame_IsIgnoredAndPreservedVerbatim(string frame)
    {
        using var driver = new FakeConsoleDriver("a" + frame + "b");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("a" + frame + "b", input);
    }

    [TestMethod]
    [DynamicData(nameof(FramedReplies))]
    public async Task EnterRawModeAsync_FragmentedFrameWithAdjacentGenuineReply_ConsumesOnlyGenuineReply(string frame)
    {
        using var driver = new FakeConsoleDriver(Bytewise("a" + frame + "\x1b[?5ub\x1b[?62c"));
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("a" + frame + "b", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_GenuineReplyBeforeFrame_DoesNotDisturbFrame()
    {
        const string paste = "\x1b[200~\x1b[?7u\x1b[201~";
        using var driver = new FakeConsoleDriver("\x1b[?5u" + paste, "\x1b[?62c");
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(paste, input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_UnterminatedFrame_IsPreservedWithoutEnablingSupport()
    {
        const string open = "\x1b[200~text \x1b[?1u\x1b[?62c";
        using var driver = new FakeConsoleDriver(open);
        var (adapter, input) = await ProbeAsync(driver);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(open, input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_PrefetchedNonUtf8Bytes_ArePreservedByteForByte()
    {
        byte[] input = [0x61, 0x1b, (byte)'[', (byte)'2', (byte)'0', (byte)'0', (byte)'~', 0xc3, 0xa9,
            0x1b, (byte)'[', (byte)'?', (byte)'1', (byte)'u', 0x1b, (byte)'[', (byte)'2', (byte)'0', (byte)'1',
            (byte)'~', 0xe2, 0x82, 0xac];
        using var driver = FakeConsoleDriver.FromByteChunks(Encoding.UTF8, input);
        var (adapter, _) = await ProbeAsyncRaw(driver);
        await using var __ = adapter;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var prefetched = await adapter.ReadInputAsync(cancellation.Token);

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        TestSeq.AreEqual(input, prefetched.ToArray());
    }

    private static async Task<(ConsolePresentationAdapter Adapter, bool Unused)> ProbeAsyncRaw(FakeConsoleDriver driver)
    {
        var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: false);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        return (adapter, false);
    }

    // --- C1 control strings. Rows are written as Latin-1 strings so each char is one byte:
    // "\u00c2\u009d" is the UTF-8 encoding of U+009D (OSC) and "\u009d" a raw 8-bit OSC. ---

    private const string Utf8Osc = "\u00c2\u009d";
    private const string Utf8Dcs = "\u00c2\u0090";
    private const string Utf8Apc = "\u00c2\u009f";
    private const string Utf8St = "\u00c2\u009c";

    public static IEnumerable<object[]> C1FramedReplies =>
    [
        ["UTF-8 OSC, UTF-8 ST", Utf8Osc + "0;title \u001b[?1u\u001b[?62c" + Utf8St],
        ["UTF-8 APC, UTF-8 ST", Utf8Apc + "Gi=7;\u001b[?31u" + Utf8St],
        ["UTF-8 DCS, UTF-8 ST", Utf8Dcs + "1$r\u001b[?31u" + Utf8St],
        ["UTF-8 OSC, ESC ST", Utf8Osc + "0;t\u001b[?31u\u001b\\"],
        ["UTF-8 OSC, BEL", Utf8Osc + "0;t\u001b[?31u\u0007"],
        ["UTF-8 DCS, raw ST", Utf8Dcs + "q\u001b[?31u\u009c"],
        ["ESC OSC, UTF-8 ST", "\u001b]0;\u001b[?31u" + Utf8St],
        ["ESC OSC with U+00DC (C3 9C) inside", "\u001b]0;\u00c3\u009c\u001b[?31u\u001b\\"],
        ["raw OSC, raw ST", "\u009d0;t\u001b[?31u\u009c"],
        ["raw APC, raw ST", "\u009fG\u001b[?31u\u009c"],
        ["raw DCS, ESC ST", "\u0090q\u001b[?31u\u001b\\"],
    ];

    private static byte[] Latin1(string text) => Encoding.Latin1.GetBytes(text);

    private static async Task<(ConsolePresentationAdapter Adapter, string Input)> ProbeBytesAsync(
        Encoding encoding, params byte[][] chunks)
    {
        var driver = FakeConsoleDriver.FromByteChunks(encoding, chunks);
        var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: true);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var bytes = await adapter.ReadInputAsync(cancellation.Token);
        // UTF-8 input passes through unchanged; other code pages are normalized to UTF-8.
        var input = encoding is UTF8Encoding ? Encoding.Latin1.GetString(bytes.Span) : Encoding.UTF8.GetString(bytes.Span);
        return (adapter, input);
    }

    [TestMethod]
    [DynamicData(nameof(C1FramedReplies))]
    public async Task EnterRawModeAsync_ReplyInsideC1Frame_IsIgnoredAndPreservedVerbatim(string name, string frame)
    {
        var (adapter, input) = await ProbeBytesAsync(Encoding.UTF8, Latin1("a" + frame + "b"));
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard, name);
        Assert.AreEqual("a" + frame + "b", input, name);
    }

    [TestMethod]
    [DynamicData(nameof(C1FramedReplies))]
    public async Task EnterRawModeAsync_ByteSplitC1FrameWithAdjacentGenuineReply_ConsumesOnlyGenuineReply(string name, string frame)
    {
        // One byte per read splits every UTF-8 C1 pair (C2 | 9D, C2 | 9C) across reads.
        var chunks = Latin1("a" + frame + "\u001b[?31ub\u001b[?62c").Select(b => new[] { b }).ToArray();
        var (adapter, input) = await ProbeBytesAsync(Encoding.UTF8, chunks);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard, name);
        Assert.AreEqual("a" + frame + "b", input, name);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_TitleEndedByUtf8St_ThenGenuineReply_DetectsSupportAndKeepsTitle()
    {
        const string title = "\u001b]0;title" + Utf8St;
        var (adapter, input) = await ProbeBytesAsync(
            Encoding.UTF8, Latin1(title + "\u001b[?31u"), Latin1("\u001b[?62c"));
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(title, input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_C1IntroducedReply_IsNotAcceptedAsGenuine()
    {
        // Replies are only recognised in their ESC-introduced form; an 8-bit CSI stays input.
        const string reply = "\u00c2\u009b?31u";
        var (adapter, input) = await ProbeBytesAsync(Encoding.UTF8, Latin1("a" + reply + "b"));
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("a" + reply + "b", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_SingleByteCodePage_TreatsRawC1AsStringControl()
    {
        const string frame = "\u009d0;t\u001b[?1u\u009c";
        var (adapter, input) = await ProbeBytesAsync(Encoding.Latin1, Latin1("a" + frame + "\u001b[?31ub"));
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("a" + frame + "b", input);
    }

    [TestMethod]
    [DataRow(437, 0x9d, "\u00a5", false)]
    [DataRow(437, 0x9d, "\u00a5", true)]
    [DataRow(1252, 0x9f, "\u0178", false)]
    [DataRow(1252, 0x9f, "\u0178", true)]
    public async Task EnterRawModeAsync_CodePagePrintableByte_DoesNotHideGenuineReply(
        int codePage, int value, string expected, bool bytewise)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] bytes = [(byte)value, .. Encoding.ASCII.GetBytes("\u001b[?31u\u001b[?62c")];
        var chunks = bytewise ? bytes.Select(b => new[] { b }).ToArray() : [bytes];
        var (adapter, input) = await ProbeBytesAsync(Encoding.GetEncoding(codePage), chunks);
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(expected, input);
    }

    [TestMethod]
    [DataRow(437, "\u00a3", false)]
    [DataRow(437, "\u00a3", true)]
    [DataRow(1252, "\u0153", false)]
    [DataRow(1252, "\u0153", true)]
    public async Task EnterRawModeAsync_CodePagePrintableByte_DoesNotTerminateOpaqueString(
        int codePage, string expected, bool bytewise)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        byte[] bytes = [.. Encoding.ASCII.GetBytes("\u001b]0;title"), 0x9c,
            .. Encoding.ASCII.GetBytes("\u001b[?31u\u001b\\")];
        var chunks = bytewise ? bytes.Select(b => new[] { b }).ToArray() : [bytes];
        var (adapter, input) = await ProbeBytesAsync(Encoding.GetEncoding(codePage), chunks);
        await using var _ = adapter;

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("\u001b]0;title" + expected + "\u001b[?31u\u001b\\", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_CodePageMappedC1_RemainsOpaque()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        const string frame = "\u009d0;title\u001b[?1u\u001b\\";
        var (adapter, input) = await ProbeBytesAsync(
            Encoding.GetEncoding(1252), Latin1(frame + "\u001b[?31ux"));
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(frame + "x", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_StrictAscii_ProbesWithoutDecodingUnmappedBytesAsInput()
    {
        var encoding = (Encoding)Encoding.ASCII.Clone();
        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        var (adapter, input) = await ProbeBytesAsync(encoding, Encoding.ASCII.GetBytes("a\u001b[?31ub"));
        await using var _ = adapter;

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual("ab", input);
    }

    [TestMethod]
    public async Task EnterRawModeAsync_MultiByteCodePage_DoesNotTreatCharacterBytesAsC1()
    {
        // In GBK, 9D 9C is one character; it must not open an OSC that hides the genuine reply.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var gbk = Encoding.GetEncoding(936);
        byte[] text = [0x9d, 0x9c];
        var driver = FakeConsoleDriver.FromByteChunks(
            gbk, [.. text, .. Encoding.ASCII.GetBytes("\u001b[?31u\u001b[?62c")]);
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: true);
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var input = await adapter.ReadInputAsync(cancellation.Token);

        Assert.IsTrue(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.AreEqual(gbk.GetString(text), Encoding.UTF8.GetString(input.Span));
    }

    // --- Every probe consumer respects the same opaque frames: reply-shaped bytes for the
    // mouse, graphics, background, window-size and cell-size probes inside bracketed paste,
    // OSC or DCS stay input byte-for-byte and do not answer the probe. ---

    public static IEnumerable<object[]> OtherProbeRepliesInFrames
    {
        get
        {
            string[] replies =
            [
                "\x1b[?1003;1$y",
                "\x1b[?1006;0$y",
                "\x1b_Gi=2147483647;OK\x1b\\",
                "\x1b]11;rgb:ffff/0000/0000\x1b\\",
                "\x1b]11;rgb:ffff/0000/0000\x07",
                "\x1b[6;20;10t",
                "\x1b[4;480;640t",
                "\x1b[8;24;80t",
                "\x1b]1337;ReportCellSize=20;10\x07",
            ];
            foreach (var reply in replies)
            {
                yield return [$"\x1b[200~alpha{reply}beta\x1b[201~"];
                // OSC and DCS strings cannot contain their own ST, so only BEL-free ESC
                // sequences are embedded in a BEL-terminated OSC.
                if (!reply.Contains('\x07') && !reply.Contains("\x1b\\", StringComparison.Ordinal))
                {
                    yield return [$"\x1b]52;c;{reply}\x07"];
                    yield return [$"\x1bP1$r{reply}\x1b\\"];
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(OtherProbeRepliesInFrames))]
    public async Task EnterRawModeAsync_OtherProbeReplyInsideFrame_IsPreservedAndDoesNotAnswerProbe(string frame)
    {
        using var driver = new FakeConsoleDriver("a" + frame + "b");
        var adapter = new ConsolePresentationAdapter(
            driver, enableMouse: true, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: true);
        await using var _ = adapter;
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        var input = await DrainPrefetchedAsync(adapter);

        Assert.AreEqual("a" + frame + "b", input);
        Assert.IsFalse(adapter.Capabilities.SupportsKgp);
        Assert.AreEqual(0x000000, adapter.Capabilities.DefaultBackground);
        Assert.IsTrue(adapter.Capabilities.SupportsMouse, "An unanswered mouse probe keeps mouse enabled.");
    }

    [TestMethod]
    public async Task EnterRawModeAsync_PastedMouseReply_IsPreservedExactlyWhileAdjacentRepliesAreConsumed()
    {
        const string paste = "\x1b[200~alpha\x1b[?1003;1$ybeta\x1b[201~";
        using var driver = new FakeConsoleDriver(Bytewise(
            paste + "\x1b[?1003;0$y\x1b[?1006;2$y\x1b_Gi=2147483647;OK\x1b\\\x1b]11;rgb:0000/ffff/0000\x07z"));
        var adapter = new ConsolePresentationAdapter(
            driver, enableMouse: true, kgpProbeTimeout: ShortProbe, kittyDa1Conclusive: true);
        await using var _ = adapter;
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        var input = await DrainPrefetchedAsync(adapter);

        Assert.AreEqual(paste + "z", input);
        Assert.IsFalse(adapter.Capabilities.SupportsMouse, "The genuine 1003 reply (status 0) decides mouse support.");
        Assert.IsTrue(adapter.Capabilities.SupportsKgp);
        Assert.AreEqual(0x00ff00, adapter.Capabilities.DefaultBackground);
    }

    [TestMethod]
    [DataRow(false, true, DisplayName = "Kitty then DA1")]
    [DataRow(true, false, DisplayName = "DA1 then Kitty, spec-conclusive DA1")]
    public async Task EnterRawModeAsync_SixelDeclared_StillSendsDa1AndFollowsReplyOrder(bool da1First, bool expected)
    {
        const string kitty = "\x1b[?1u";
        const string da1 = "\x1b[?62;4c";
        using var driver = new FakeConsoleDriver(da1First ? da1 + kitty + "x" : kitty + da1 + "x");
        var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: ShortProbe)
            .WithSixelSupport(SixelPresentationSupport.None);
        await using var _ = adapter;
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        var input = await DrainPrefetchedAsync(adapter);

        var written = driver.WrittenText;
        var kittyQuery = written.IndexOf("\x1b[?u", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, kittyQuery);
        Assert.IsGreaterThan(kittyQuery, written.IndexOf("\x1b[c", StringComparison.Ordinal));
        Assert.AreEqual(expected, adapter.Capabilities.SupportsKittyKeyboard);
        // The declaration still wins over DA1's Sixel attribute.
        Assert.AreEqual(SixelPresentationSupport.None, adapter.Capabilities.SixelSupport);
        Assert.AreEqual("x", input, "Both replies belong to the probe and must not reach the app.");
    }

    [TestMethod]
    public async Task EnterRawModeAsync_UnsupportedHost_FallsBackWithinDefaultDeadline()
    {
        using var driver = new FakeConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(driver, kittyDa1Conclusive: false);
        var stopwatch = Stopwatch.StartNew();
        await adapter.EnterRawModeAsync(TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
        Assert.IsLessThan(TimeSpan.FromSeconds(2), stopwatch.Elapsed,
            "Silence must fall back at the existing combined probe deadline (~150ms).");
    }

    [TestMethod]
    public async Task EnterRawModeAsync_CallerCancellation_LeavesSupportDisabled()
    {
        using var driver = new FakeConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(
            driver, kgpProbeTimeout: TimeSpan.FromSeconds(30), kittyDa1Conclusive: false);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        var stopwatch = Stopwatch.StartNew();
        await adapter.EnterRawModeAsync(cancellation.Token);
        Assert.IsLessThan(TimeSpan.FromSeconds(5), stopwatch.Elapsed);
        Assert.IsFalse(adapter.Capabilities.SupportsKittyKeyboard);
    }

    // --- Actual app/console path: support probe, then layer-4 push + effective-flags query ---

    [TestMethod]
    [DataRow(31, true)]
    [DataRow(27, true)]
    [DataRow(10, true)]
    [DataRow(15, true)]
    [DataRow(8, false)]
    [DataRow(2, false)]
    [DataRow(1, false)]
    [DataRow(0, false)]
    public async Task App_ConsoleHost_EffectiveFlagsDecideReleaseGuarantee(int effective, bool releases)
    {
        await using var host = new KittyConsoleHost { EffectiveFlags = effective };
        var app = await RunAppAsync(host, async (app, _) =>
        {
            await WaitForAsync(() => app.InputSourceReportsKeyReleases == releases, "effective flags applied");
        });

        AssertBalanced(host, expectedPushes: 1);
        Assert.AreEqual(releases, host.ReleaseReportsObserved);
    }

    [TestMethod]
    public async Task App_ConsoleHost_ProbeReplyIsNotMistakenForEffectiveFlags()
    {
        // The support reply reports 31 (flags already active in the host) but the host never
        // answers the post-push query; releases stay unconfirmed.
        await using var host = new KittyConsoleHost { ProbeReply = "\x1b[?31u", EffectiveFlags = null };
        await RunAppAsync(host, async (app, _) =>
        {
            await WaitForAsync(() => host.Pushes == 1, "push");
            await Task.Delay(100);
            Assert.IsNull(app.InputSourceReportsKeyReleases);
        });

        AssertBalanced(host, expectedPushes: 1);
        var written = host.Written;
        var probe = written.IndexOf("\x1b[?u", StringComparison.Ordinal);
        var push = written.IndexOf("\x1b[>31u\x1b[?u", StringComparison.Ordinal);
        var pop = written.IndexOf("\x1b[<u", StringComparison.Ordinal);
        var altExit = written.LastIndexOf("\x1b[?1049l", StringComparison.Ordinal);
        Assert.IsTrue(probe >= 0 && probe < push && push < pop && pop < altExit,
            $"Expected probe < push+query < pop < alternate-screen exit: {Escape(written)}");
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task App_ConPtyOrdering_Da1BeforeKittyReply_StillPushes(bool da1First)
    {
        await using var host = new KittyConsoleHost { Da1First = da1First, Da1Conclusive = false };
        await RunAppAsync(host, async (app, _) =>
            await WaitForAsync(() => app.InputSourceReportsKeyReleases == true, "releases confirmed"));

        AssertBalanced(host, expectedPushes: 1);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "silent host")]
    [DataRow("\x1b[?;u", DisplayName = "malformed reply")]
    public async Task App_UnsupportedConsoleHost_NeverPushesOrPops(string? probeReply)
    {
        await using var host = new KittyConsoleHost { ProbeReply = probeReply, Da1Conclusive = false };
        await RunAppAsync(host, async (app, _) =>
        {
            await WaitForAsync(() => host.Written.Contains("\x1b[?1049h", StringComparison.Ordinal), "tui entered");
            Assert.IsNull(app.InputSourceReportsKeyReleases);
        });

        Assert.AreEqual(0, host.Pushes);
        Assert.AreEqual(0, host.Pops);
        Assert.DoesNotContain("\x1b[>31u", host.Written);
        Assert.DoesNotContain("\x1b[<u", host.Written);
        Assert.AreEqual(4, host.MainFlags);
        Assert.AreEqual(8, host.AlternateFlags);
    }

    [TestMethod]
    public async Task App_ConsoleHost_CancellationPopsBeforeLeavingAlternateScreen()
    {
        await using var host = new KittyConsoleHost();
        await RunAppAsync(host, async (_, cancel) =>
        {
            await WaitForAsync(() => host.Pushes == 1, "push");
            cancel();
        }, stopByCancellation: true);

        AssertBalanced(host, expectedPushes: 1);
    }

    [TestMethod]
    public async Task App_ConsoleHost_FailedPushWrite_DoesNotLaterPop()
    {
        await using var host = new KittyConsoleHost { FailPush = true };
        Hex1bApp? app = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(_ => { }, instance =>
            {
                app = instance;
                return context => context.Text("Kitty console");
            })
            .WithPresentation(host.Adapter).WithDimensions(30, 6).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = terminal.RunAsync(cancellation.Token);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.AreEqual(0, host.Pushes);
        Assert.AreEqual(0, host.Pops, "A rejected push must not be followed by an unbalanced pop.");
        Assert.AreEqual(4, host.MainFlags);
        Assert.AreEqual(8, host.AlternateFlags);
    }

    private static void AssertBalanced(KittyConsoleHost host, int expectedPushes)
    {
        Assert.AreEqual(expectedPushes, host.Pushes);
        Assert.AreEqual(expectedPushes, host.Pops);
        Assert.IsTrue(host.PushesOnAlternate == expectedPushes && host.PopsOnAlternate == expectedPushes,
            "Push and pop must both target the alternate screen's stack.");
        Assert.AreEqual(4, host.MainFlags, "The main screen's stack must be untouched.");
        Assert.AreEqual(8, host.AlternateFlags, "The alternate screen's stack must be restored.");
    }

    private static async Task<Hex1bApp> RunAppAsync(
        KittyConsoleHost host, Func<Hex1bApp, Action, Task> body, bool stopByCancellation = false)
    {
        Hex1bApp? app = null;
        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(_ => { }, instance =>
            {
                app = instance;
                return context => context.Text("Kitty console");
            })
            .WithPresentation(host.Adapter).WithDimensions(30, 6).Build();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = terminal.RunAsync(cancellation.Token);
        try
        {
            await WaitForAsync(() => app is not null && host.Written.Contains("\x1b[?1049h", StringComparison.Ordinal),
                "app started");
            await body(app!, cancellation.Cancel);
            if (!stopByCancellation)
                app!.RequestStop();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException) when (stopByCancellation)
            {
            }
        }
        finally
        {
            cancellation.Cancel();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
            }
        }
        return app!;
    }

    private static async Task WaitForAsync(Func<bool> condition, string description)
    {
        var deadline = Stopwatch.StartNew();
        while (!condition())
        {
            if (deadline.Elapsed > TimeSpan.FromSeconds(5))
                Assert.Fail($"Timed out waiting for {description}.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    private static string Escape(string text) => text.Replace("\x1b", "\\e");

    /// <summary>
    /// A scripted outer terminal behind the real <see cref="ConsolePresentationAdapter"/>.
    /// It tracks Kitty keyboard stacks per screen in a headless Hex1b terminal (main preset
    /// to 4, alternate to 8), answers the support probe and DA1 in a chosen order, and
    /// answers the post-push effective-flags query independently.
    /// </summary>
    private sealed class KittyConsoleHost : IConsoleDriver, IAsyncDisposable
    {
        private readonly Channel<byte[]> _input = Channel.CreateUnbounded<byte[]>();
        private readonly StringBuilder _written = new();
        private readonly Hex1bTerminal _state = Hex1bTerminal.CreateBuilder()
            .WithWorkload(new Hex1bAppWorkloadAdapter()).WithHeadless().Build();
        private ConsolePresentationAdapter? _adapter;
        private bool _probeAnswered;

        public KittyConsoleHost()
        {
            _state.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[=4u\x1b[?1049h\x1b[=8u\x1b[?1049l"));
        }

        public string? ProbeReply { get; init; } = "\x1b[?0u";
        public bool Da1First { get; init; }
        public bool Da1Conclusive { get; init; } = true;
        public int? EffectiveFlags { get; init; } = 31;
        public bool FailPush { get; init; }

        public ConsolePresentationAdapter Adapter => _adapter ??= new ConsolePresentationAdapter(
            this, kittyDa1Conclusive: Da1Conclusive);

        public int Pushes { get; private set; }
        public int Pops { get; private set; }
        public int PushesOnAlternate { get; private set; }
        public int PopsOnAlternate { get; private set; }
        public bool ReleaseReportsObserved { get; private set; }
        public int MainFlags
        {
            get { lock (_written) return _state.InAlternateScreen ? -1 : _state.InputModes.KittyKeyboardFlags; }
        }

        public int AlternateFlags { get; private set; } = 8;

        public string Written
        {
            get { lock (_written) return _written.ToString(); }
        }
        public void Write(ReadOnlySpan<byte> data)
        {
            var text = Encoding.UTF8.GetString(data);
            if (FailPush && text.Contains("\x1b[>31u", StringComparison.Ordinal))
                throw new IOException("push write rejected");
            lock (_written)
            {
                _written.Append(text);
                foreach (var token in AnsiTokenizer.Tokenize(text))
                {
                    var sequence = (token as UnrecognizedSequenceToken)?.Sequence;
                    var effectiveQuery = sequence == "\x1b[?u" && _probeAnswered;
                    _state.ApplyTokens([token]);
                    if (_state.InAlternateScreen)
                        AlternateFlags = _state.InputModes.KittyKeyboardFlags;
                    if (sequence == "\x1b[>31u")
                    {
                        Pushes++;
                        if (_state.InAlternateScreen) PushesOnAlternate++;
                    }
                    else if (sequence == "\x1b[<u")
                    {
                        Pops++;
                        if (_state.InAlternateScreen) PopsOnAlternate++;
                    }
                    else if (sequence == "\x1b[?u" && !effectiveQuery)
                    {
                        _probeAnswered = true;
                        if (!Da1First && ProbeReply is not null)
                            Enqueue(ProbeReply);
                    }
                    else if (effectiveQuery && EffectiveFlags is int flags)
                    {
                        ReleaseReportsObserved = (flags & 10) == 10;
                        Enqueue($"\x1b[?{flags}u");
                    }
                }

                if (text == "\x1b[c")
                {
                    Enqueue("\x1b[?61;6;7;21;22;23;24;28;32;42c");
                    if (Da1First && ProbeReply is not null)
                        Enqueue(ProbeReply);
                }
            }
        }

        private void Enqueue(string text) => _input.Writer.TryWrite(Encoding.ASCII.GetBytes(text));

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            var chunk = await _input.Reader.ReadAsync(ct);
            chunk.CopyTo(buffer);
            return chunk.Length;
        }

        public Encoding InputEncoding => Encoding.UTF8;
        public bool DataAvailable => false;
        public int Width => 30;
        public int Height => 6;
        public event Action<int, int>? Resized { add { } remove { } }
        public void EnterRawMode(bool preserveOPost = false) { }
        public void ExitRawMode() { }
        public void Flush() { }
        public void DrainInput() { }
        public bool TryGetWindowPixelSize(out int pixelWidth, out int pixelHeight)
        {
            pixelWidth = pixelHeight = 0;
            return false;
        }

        public void Dispose() { }

        public async ValueTask DisposeAsync()
        {
            if (_adapter is not null)
                await _adapter.DisposeAsync();
            await _state.DisposeAsync();
        }
    }
}
