using System.Text;
using Hex1b.Input;

namespace Hex1b.Tests;

/// <summary>
/// Exercises Kitty keyboard detection and key-event bytes across a real ConPTY boundary.
/// The parent injects VT bytes into the pseudo console; these tests do not validate
/// physical key mapping, keyboard layouts or IME composition.
/// </summary>
[TestClass]
[TestCategory("Windows")]
public class KittyConPtyTests
{
    private const string ProbeChildVariable = "HEX1B_KITTY_CONPTY_PROBE_TEST";
    private const string KeysChildVariable = "HEX1B_KITTY_CONPTY_KEYS_TEST";
    private const string KeysReady = "KITTY_KEYS_READY";

    private static readonly string[] KeyVectors =
    [
        "\x1b[97u",
        "\x1b[97;1:2u",
        "\x1b[97;1:3u",
        "\x1b[97;5u",
        "\x1b[97;2;65u",
        "\x1b[97;65u",
        "\x1b[97;129u",
        "\x1b[27;2:3u",
        "\x1b[1;1:3A",
        "\x1b[13~",
        "\x1b[57441;2u",
        "\x1b[57399u",
        "\x1b[97:65:113;2u",
        "\x1b[1092::97;1;1092u",
    ];

    [TestMethod]
    [DataRow("\x1b[?1u", true, "abc")]
    [DataRow("\x1b[?31u", true, "abc")]
    [DataRow("\x1b[?1;2u", false, "abc")]
    [DataRow(null, false, "abc")]
    [DataRow("\x1b[200~\x1b[?1u\x1b[201~", false, "^[[200~^[[?1u^[[201~abc")]
    public async Task EnterRawModeAsync_ThroughConPty_DetectsKittyAndPreservesInput(
        string? reply, bool supported, string expectedInput)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = cancellation.Token;
        await using var pty = await StartChildAsync(nameof(ProbeChild), ProbeChildVariable, ct);

        var output = new StringBuilder();
        var replied = false;
        var expected = $"KITTY_RESULT:{supported}:{expectedInput};";
        while (!output.ToString().Contains(expected, StringComparison.Ordinal))
        {
            var bytes = await pty.ReadAsync(ct);
            Assert.IsFalse(bytes.IsEmpty, $"Child exited before reporting: {Escape(output.ToString())}");
            output.Append(Encoding.UTF8.GetString(bytes.Span));
            // ConPTY forwards the Kitty query to its host; answer only after observing it.
            if (!replied && output.ToString().Contains("\x1b[?u", StringComparison.Ordinal))
            {
                replied = true;
                if (reply is not null)
                {
                    foreach (var value in Encoding.UTF8.GetBytes(reply))
                        await pty.WriteAsync(new[] { value }, ct);
                }
                await pty.WriteAsync("abcz"u8.ToArray(), ct);
            }
        }

        Assert.IsTrue(replied, "ConPTY did not forward the Kitty keyboard query.");
        Assert.AreEqual(0, await pty.WaitForExitAsync(ct), Escape(output.ToString()));
    }

    [TestMethod]
    public async Task ProbeChild()
    {
        if (Environment.GetEnvironmentVariable(ProbeChildVariable) != "1")
            return;

        using var driver = new WindowsConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(driver, kgpProbeTimeout: TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await adapter.EnterRawModeAsync(cancellation.Token);

        var input = new StringBuilder();
        while (!input.ToString().EndsWith('z'))
        {
            var bytes = await adapter.ReadInputAsync(cancellation.Token);
            Assert.IsFalse(bytes.IsEmpty);
            input.Append(Encoding.UTF8.GetString(bytes.Span));
        }

        driver.Write(Encoding.UTF8.GetBytes(
            $"KITTY_RESULT:{adapter.Capabilities.SupportsKittyKeyboard}:{Escape(input.ToString()[..^1])};\r\n"));
    }

    [TestMethod]
    public async Task KeyEvents_ThroughConPty_ArriveLiterallyAndDecode()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = cancellation.Token;
        await using var pty = await StartChildAsync(nameof(KeysChild), KeysChildVariable, ct);

        var output = new StringBuilder();
        var sent = false;
        while (!output.ToString().Contains("KITTY_KEYS_OK", StringComparison.Ordinal))
        {
            var bytes = await pty.ReadAsync(ct);
            Assert.IsFalse(bytes.IsEmpty, $"Child exited before decoding: {Escape(output.ToString())}");
            output.Append(Encoding.UTF8.GetString(bytes.Span));
            if (!sent && output.ToString().Contains(KeysReady, StringComparison.Ordinal))
            {
                sent = true;
                foreach (var vector in KeyVectors)
                    await pty.WriteAsync(Encoding.UTF8.GetBytes(vector), ct);
                await pty.WriteAsync("z"u8.ToArray(), ct);
            }
        }

        Assert.AreEqual(0, await pty.WaitForExitAsync(ct), Escape(output.ToString()));
    }

    [TestMethod]
    public async Task KeysChild()
    {
        if (Environment.GetEnvironmentVariable(KeysChildVariable) != "1")
            return;

        using var driver = new WindowsConsoleDriver();
        driver.EnterRawMode();
        driver.Write(Encoding.UTF8.GetBytes(KeysReady + "\r\n"));

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new List<byte>();
        var buffer = new byte[256];
        while (received.Count == 0 || received[^1] != (byte)'z')
        {
            var count = await driver.ReadAsync(buffer, cancellation.Token);
            Assert.IsGreaterThan(0, count);
            received.AddRange(buffer.AsSpan(0, count));
        }

        var text = Encoding.UTF8.GetString([.. received])[..^1];
        Assert.AreEqual(Escape(string.Concat(KeyVectors)), Escape(text));

        var events = new List<Hex1bKeyEvent>();
        var offset = 0;
        foreach (var vector in KeyVectors)
        {
            Assert.IsTrue(KittyKeyboardInput.TryDecode(text.Substring(offset, vector.Length), out var decoded), Escape(vector));
            events.Add(decoded);
            offset += vector.Length;
        }

        AssertKey(events[0], Hex1bKey.A, Hex1bModifiers.None, Hex1bKeyEventType.Press, 97);
        AssertKey(events[1], Hex1bKey.A, Hex1bModifiers.None, Hex1bKeyEventType.Repeat, 97);
        AssertKey(events[2], Hex1bKey.A, Hex1bModifiers.None, Hex1bKeyEventType.Release, 97);
        AssertKey(events[3], Hex1bKey.A, Hex1bModifiers.Control, Hex1bKeyEventType.Press, 97);
        AssertKey(events[4], Hex1bKey.A, Hex1bModifiers.Shift, Hex1bKeyEventType.Press, 97);
        Assert.AreEqual("A", events[4].Text);
        AssertKey(events[5], Hex1bKey.A, Hex1bModifiers.CapsLock, Hex1bKeyEventType.Press, 97);
        AssertKey(events[6], Hex1bKey.A, Hex1bModifiers.NumLock, Hex1bKeyEventType.Press, 97);
        AssertKey(events[7], Hex1bKey.Escape, Hex1bModifiers.Shift, Hex1bKeyEventType.Release, 27);
        Assert.AreEqual(Hex1bKey.UpArrow, events[8].Key);
        Assert.AreEqual(Hex1bKeyEventType.Release, events[8].EventType);
        Assert.AreEqual(Hex1bKey.F3, events[9].Key);
        Assert.AreEqual(Hex1bKeyEventType.Press, events[9].EventType);
        Assert.AreEqual(57441, events[10].UnicodeKeyCode);
        Assert.AreEqual(Hex1bModifiers.Shift, events[10].Modifiers);
        Assert.AreEqual(57399, events[11].UnicodeKeyCode);
        Assert.AreEqual(65, events[12].ShiftedKeyCode);
        Assert.AreEqual(113, events[12].BaseLayoutKeyCode);
        Assert.AreEqual(Hex1bModifiers.Shift, events[12].Modifiers);
        Assert.AreEqual(1092, events[13].UnicodeKeyCode);
        Assert.IsNull(events[13].ShiftedKeyCode);
        Assert.AreEqual(97, events[13].BaseLayoutKeyCode);
        Assert.AreEqual("\u0444", events[13].Text);

        driver.Write(Encoding.UTF8.GetBytes("KITTY_KEYS_OK\r\n"));
    }

    private static void AssertKey(
        Hex1bKeyEvent input, Hex1bKey key, Hex1bModifiers modifiers, Hex1bKeyEventType type, int code)
    {
        Assert.AreEqual(key, input.Key);
        Assert.AreEqual(modifiers, input.Modifiers);
        Assert.AreEqual(type, input.EventType);
        Assert.AreEqual(code, input.UnicodeKeyCode);
    }

    private static async Task<WindowsProxyPtyHandle> StartChildAsync(string child, string variable, CancellationToken ct)
    {
        var pty = new WindowsProxyPtyHandle();
        await pty.StartAsync(
            Path.Combine(AppContext.BaseDirectory, "Hex1b.Tests.exe"),
            ["--filter", $"FullyQualifiedName=Hex1b.Tests.{nameof(KittyConPtyTests)}.{child}", "--no-progress", "--no-ansi"],
            AppContext.BaseDirectory,
            new Dictionary<string, string> { [variable] = "1" },
            80, 24, ct);
        return pty;
    }

    private static string Escape(string value) => value.Replace("\x1b", "^[", StringComparison.Ordinal);
}
