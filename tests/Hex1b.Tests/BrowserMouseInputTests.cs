using System.Text;
using System.Text.Json;
using Hex1b.Tokens;

namespace Hex1b.Tests;

[TestClass]
public class BrowserMouseInputTests
{
    [TestMethod]
    [DataRow(0, "down", "left", false)]
    [DataRow(9, "down", "left", true)]
    [DataRow(9, "up", "left", false)]
    [DataRow(9, "wheel", "wheelUp", false)]
    [DataRow(9, "move", "left", false)]
    [DataRow(1000, "down", "right", true)]
    [DataRow(1000, "up", "right", true)]
    [DataRow(1000, "move", "left", false)]
    [DataRow(1000, "move", "none", false)]
    [DataRow(1000, "wheel", "wheelDown", true)]
    [DataRow(1002, "move", "left", true)]
    [DataRow(1002, "move", "none", false)]
    [DataRow(1003, "move", "none", true)]
    [DataRow(1003, "move", "middle", true)]
    public void EncodeMouse_TrackingMode_FiltersUnrequestedEvents(
        int mode, string action, string button, bool reported)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, mode, 1006);
        Assert.AreEqual(reported, Encode(terminal, action, button).Length > 0);
    }

    [TestMethod]
    [DataRow("down", "left", "\x1b[<28;5;3M")]
    [DataRow("up", "right", "\x1b[<30;5;3m")]
    [DataRow("move", "left", "\x1b[<60;5;3M")]
    [DataRow("move", "none", "\x1b[<63;5;3M")]
    [DataRow("wheel", "wheelUp", "\x1b[<92;5;3M")]
    [DataRow("wheel", "wheelDown", "\x1b[<93;5;3M")]
    [DataRow("wheel", "wheelLeft", "\x1b[<94;5;3M")]
    [DataRow("wheel", "wheelRight", "\x1b[<95;5;3M")]
    public void EncodeMouse_Sgr_PreservesButtonsModifiersAndOneBasedCoordinates(
        string action, string button, string expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, 1003, 1006);
        TestSeq.AreEqual(Encoding.ASCII.GetBytes(expected),
            Encode(terminal, action, button, modifiers: true));
    }

    [TestMethod]
    public void EncodeMouse_Legacy_PreservesHighCoordinateBytesWithoutUtf8Expansion()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, 1000, 0);
        TestSeq.AreEqual(new byte[] { 27, 91, 77, 32, 255, 128 },
            Encode(terminal, "down", "left", 222, 95));
        TestSeq.AreEqual(new byte[] { 27, 91, 77, 63, 37, 35 },
            Encode(terminal, "up", "right", modifiers: true));
        Assert.AreEqual(0, Encode(terminal, "down", "left", 223, 0).Length);
    }

    [TestMethod]
    [DataRow(1005, "\x1b[M \u011b!")]
    [DataRow(1015, "\x1b[32;251;1M")]
    [DataRow(1006, "\x1b[<0;251;1M")]
    public void EncodeMouse_ExtendedEncoding_ReportsColumnsBeyondLegacyLimit(int encoding, string expected)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, 1000, encoding);
        TestSeq.AreEqual(Encoding.UTF8.GetBytes(expected),
            Encode(terminal, "down", "left", 250, 0));
    }

    [TestMethod]
    public void EncodeMouse_X10_DoesNotReportModifiers()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, 9, 0);
        TestSeq.AreEqual(new byte[] { 27, 91, 77, 34, 37, 35 },
            Encode(terminal, "down", "right", modifiers: true));
    }

    [TestMethod]
    public void EncodeMouse_WheelBatch_EmitsRequestedNumberOfReports()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, 1000, 1006);
        Assert.AreEqual("\x1b[<65;5;3M\x1b[<65;5;3M\x1b[<65;5;3M",
            Encoding.ASCII.GetString(Encode(terminal, "wheel", "wheelDown", count: 3)));
    }

    [TestMethod]
    public void EncodeMouse_ModeChangeAndResize_DropsStaleInput()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, 1003, 1006);
        Assert.IsTrue(Encode(terminal, "move", "none", 250, 0).Length > 0);
        terminal.Resize(80, 24);
        Assert.AreEqual(0, Encode(terminal, "move", "none", 250, 0).Length);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1003l"));
        Assert.AreEqual(0, Encode(terminal, "down", "left").Length);
    }

    [TestMethod]
    public void EncodeMouse_ConcurrentEncodingFlags_PrefersSgrThenUrxvtThenUtf8()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1000;1005;1006;1015h"));

        Assert.AreEqual("\x1b[<0;251;1M", Encoding.UTF8.GetString(Encode(terminal, "down", "left", 250, 0)));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1006l"));
        Assert.AreEqual("\x1b[32;251;1M", Encoding.UTF8.GetString(Encode(terminal, "down", "left", 250, 0)));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1015l"));
        Assert.AreEqual("\x1b[M \u011b!", Encoding.UTF8.GetString(Encode(terminal, "down", "left", 250, 0)));
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?1005l"));
        Assert.AreEqual(0, Encode(terminal, "down", "left", 250, 0).Length);
    }

    [TestMethod]
    public void MouseTracking_ConcurrentTrackingFlags_UsesSnapshotPriority()
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        terminal.ApplyTokens(AnsiTokenizer.Tokenize("\x1b[?9;1000;1002;1003h"));
        using var snapshot = terminal.CreateSnapshot();
        Assert.AreEqual(1003, Hwt1Input.MouseTracking(snapshot));

        foreach (var (disabled, expected) in new[] { (1003, 1002), (1002, 1000), (1000, 9), (9, 0) })
        {
            terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{disabled}l"));
            using var current = terminal.CreateSnapshot();
            Assert.AreEqual(expected, Hwt1Input.MouseTracking(current));
        }
        Assert.AreEqual(1003, Hwt1Input.MouseTracking(snapshot));
    }

    [TestMethod]
    [DataRow("wheelLeft", 66)]
    [DataRow("wheelRight", 67)]
    public void EncodeMouse_MaximumWheelBatch_PreservesHorizontalDirection(string button, int code)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        SetMode(terminal, 1000, 1006);

        var expected = string.Concat(Enumerable.Repeat($"\x1b[<{code};5;3M", 32));
        Assert.AreEqual(expected, Encoding.ASCII.GetString(Encode(terminal, "wheel", button, count: 32)));
    }

    [TestMethod]
    [DataRow("ctrl", "1")]
    [DataRow("alt", "null")]
    [DataRow("shift", "\"false\"")]
    public void EncodeMouse_MalformedModifier_RejectsCommandEvenWithTrackingDisabled(string flag, string value)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        using var command = JsonDocument.Parse(
            $$"""{"action":"down","button":"left","x":0,"y":0,"{{flag}}":{{value}}}""");

        Assert.ThrowsExactly<InvalidDataException>(() => Hwt1Input.EncodeMouse(command.RootElement, terminal));
    }

    [TestMethod]
    [DataRow("click", "left", 0, 0, 1)]
    [DataRow("down", "none", 0, 0, 1)]
    [DataRow("wheel", "left", 0, 0, 1)]
    [DataRow("down", "wheelUp", 0, 0, 1)]
    [DataRow("down", "left", -1, 0, 1)]
    [DataRow("down", "left", 1024, 0, 1)]
    [DataRow("down", "left", 0, 512, 1)]
    [DataRow("down", "left", 0, 0, 2)]
    [DataRow("wheel", "wheelUp", 0, 0, 0)]
    [DataRow("wheel", "wheelUp", 0, 0, 33)]
    public void EncodeMouse_InvalidIntent_RejectsCommand(
        string action, string button, int x, int y, int count)
    {
        using var workload = new Hex1bAppWorkloadAdapter();
        using var terminal = CreateTerminal(workload);
        Assert.ThrowsExactly<InvalidDataException>(() => Encode(terminal, action, button, x, y, count));
    }

    private static Hex1bTerminal CreateTerminal(Hex1bAppWorkloadAdapter workload)
        => Hex1bTerminal.CreateBuilder().WithWorkload(workload)
            .WithHeadless().WithDimensions(300, 100).Build();

    private static void SetMode(Hex1bTerminal terminal, int tracking, int encoding)
    {
        if (tracking != 0) terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{tracking}h"));
        if (encoding != 0) terminal.ApplyTokens(AnsiTokenizer.Tokenize($"\x1b[?{encoding}h"));
    }

    private static byte[] Encode(Hex1bTerminal terminal, string action, string button,
        int x = 4, int y = 2, int count = 1, bool modifiers = false)
    {
        using var command = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            action, button, x, y, count, shift = modifiers, alt = modifiers, ctrl = modifiers
        }));
        return Hwt1Input.EncodeMouse(command.RootElement, terminal);
    }
}
