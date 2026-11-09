using System.Text;
using System.Runtime.InteropServices;

namespace Hex1b.Tests;

[TestClass]
public class WindowsConsoleProbeTests
{
    private const string ChildEnvironmentVariable = "HEX1B_WINDOWS_CONSOLE_PROBE_TEST";
    private const string ProbeStarted = "PROBE_STARTED";
    internal const string PtyIoChildEnvironmentVariable = "HEX1B_WINDOWS_PTY_IO_TEST";
    private const string MouseChildEnvironmentVariable = "HEX1B_WINDOWS_MOUSE_TEST";

    [TestMethod]
    [TestCategory("Windows")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MouseModes_ThroughConPty_ControlsNativeReportsAndPreservesVtInput(bool disableVtInput)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = cancellation.Token;
        await using var pty = new WindowsProxyPtyHandle();
        await pty.StartAsync(
            Path.Combine(AppContext.BaseDirectory, "Hex1b.Tests.exe"),
            ["--filter", "FullyQualifiedName=Hex1b.Tests.WindowsConsoleProbeTests.MouseChild", "--no-progress", "--no-ansi"],
            AppContext.BaseDirectory,
            new Dictionary<string, string> { [MouseChildEnvironmentVariable] = disableVtInput ? "fallback" : "vt" },
            80, 24, ct);

        var output = new StringBuilder();
        var sent = false;
        while (!output.ToString().Contains("MOUSE_RESULT:OK", StringComparison.Ordinal))
        {
            var bytes = await pty.ReadAsync(ct);
            Assert.IsFalse(bytes.IsEmpty, $"Mouse child exited before success: {output}");
            output.Append(Encoding.UTF8.GetString(bytes.Span));
            if (!sent && output.ToString().Contains("MOUSE_VT_READY", StringComparison.Ordinal))
            {
                sent = true;
                await pty.WriteAsync("\x1b[<0;3;4M\x1b[<0;3;4mz"u8.ToArray(), ct);
            }
        }
        Assert.IsTrue(sent);
        Assert.AreEqual(0, await pty.WaitForExitAsync(ct), output.ToString());
    }

    [TestMethod]
    public async Task MouseChild()
    {
        var childMode = Environment.GetEnvironmentVariable(MouseChildEnvironmentVariable);
        if (childMode is null)
            return;

        using var driver = new WindowsConsoleDriver();
        var inputHandle = GetStdHandle(-10);
        Assert.IsTrue(GetConsoleMode(inputHandle, out var originalMode));
        driver.EnterRawMode();
        if (childMode == "fallback")
        {
            Assert.IsTrue(GetConsoleMode(inputHandle, out var mode));
            Assert.IsTrue(SetConsoleMode(inputHandle, mode & ~0x0200u));
        }
        AssertMouseCapture(inputHandle, enabled: false);
        Assert.AreEqual("", await ReadNativeMouseAsync(driver, inputHandle, MouseRecord(1)));

        driver.Write("\x1b[?100"u8);
        AssertMouseCapture(inputHandle, enabled: false);
        driver.Write("3;1006h"u8);
        AssertMouseCapture(inputHandle, enabled: true);
        Assert.AreEqual(
            "\x1b[<0;3;4M\x1b[<32;3;4M\x1b[<0;3;4m\x1b[<35;3;4M\x1b[<64;3;4M",
            await ReadNativeMouseAsync(driver, inputHandle,
                MouseRecord(1), MouseRecord(1, 1), MouseRecord(0), MouseRecord(0, 1),
                MouseRecord(120u << 16, 4)));
        Assert.AreEqual("\x1b[<0;3;4M",
            await ReadNativeMouseAsync(driver, inputHandle, MouseRecord(1)));
        driver.Write("\x1b[?1003l\x1b[?1003h"u8);
        Assert.AreEqual("\x1b[<0;3;4M",
            await ReadNativeMouseAsync(driver, inputHandle, MouseRecord(1)));

        driver.Write("\x1b[?1003l"u8);
        AssertMouseCapture(inputHandle, enabled: false);
        Assert.AreEqual("", await ReadNativeMouseAsync(driver, inputHandle, MouseRecord(1)));

        driver.Write("\x1b[?1002h"u8);
        Assert.AreEqual("\x1b[<0;3;4M\x1b[<32;3;4M\x1b[<0;3;4m",
            await ReadNativeMouseAsync(driver, inputHandle,
                MouseRecord(0, 1), MouseRecord(1), MouseRecord(1, 1), MouseRecord(0)));

        driver.Write("\x1b[?1002l\x1b[?1000h"u8);
        Assert.AreEqual("\x1b[<0;3;4M\x1b[<0;3;4m",
            await ReadNativeMouseAsync(driver, inputHandle,
                MouseRecord(0, 1), MouseRecord(1), MouseRecord(1, 1), MouseRecord(0)));

        driver.Write("\x1b[?1000l"u8);
        driver.Write("\x1b]title;\x1b[?1003h\x07"u8);
        AssertMouseCapture(inputHandle, enabled: false);
        driver.Write("\x1b[?1003;1006h"u8);
        driver.Write("MOUSE_VT_READY\r\n"u8);
        var forwarded = await ReadUntilSentinelAsync(driver);
        Assert.AreEqual("\x1b[<0;3;4M\x1b[<0;3;4m", forwarded);
        driver.Write("\x1b[?1003;1006l"u8);
        AssertMouseCapture(inputHandle, enabled: false);
        driver.ExitRawMode();
        Assert.IsTrue(GetConsoleMode(inputHandle, out var restoredMode));
        Assert.AreEqual(originalMode, restoredMode);
        driver.EnterRawMode();
        driver.Write("\x1b[?1003;1006h"u8);
        driver.ExitRawMode();
        driver.EnterRawMode();
        AssertMouseCapture(inputHandle, enabled: false);
        Assert.AreEqual("", await ReadNativeMouseAsync(driver, inputHandle, MouseRecord(1)));
        driver.Write("\x1b[?1003;1006h"u8);
        AssertMouseCapture(inputHandle, enabled: true);
        Assert.AreEqual("\x1b[<0;3;4M",
            await ReadNativeMouseAsync(driver, inputHandle, MouseRecord(1)));
        driver.Write("\x1b[?1003;1006l"u8);
        driver.ExitRawMode();
        driver.Write("MOUSE_RESULT:OK\r\n"u8);
    }

    private static void AssertMouseCapture(nint inputHandle, bool enabled)
    {
        Assert.IsTrue(GetConsoleMode(inputHandle, out var mode));
        Assert.AreEqual(enabled, (mode & 0x0010) != 0, $"Console input mode: {mode:X}");
        Assert.AreNotEqual(0u, mode & 0x0008, "Resize events must remain enabled.");
    }

    private static ConsoleInputRecord MouseRecord(uint buttons, uint flags = 0) => new()
    {
        EventType = 2, X = 2, Y = 3, Buttons = buttons, Flags = flags
    };

    private static async Task<string> ReadNativeMouseAsync(
        WindowsConsoleDriver driver, nint inputHandle, params ConsoleInputRecord[] mouseRecords)
    {
        ConsoleInputRecord[] records = [.. mouseRecords, new()
        {
            EventType = 1, KeyDown = 1, RepeatCount = 1, Character = 'z'
        }];
        Assert.IsTrue(WriteConsoleInput(inputHandle, records, (uint)records.Length, out var written));
        Assert.AreEqual((uint)records.Length, written);
        return await ReadUntilSentinelAsync(driver);
    }

    private static async Task<string> ReadUntilSentinelAsync(WindowsConsoleDriver driver)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(5));
        var input = new StringBuilder();
        var buffer = new byte[256];
        while (true)
        {
            var count = await driver.ReadAsync(buffer, cancellation.Token);
            Assert.IsTrue(count > 0, "Mouse input ended before the keyboard sentinel.");
            input.Append(Encoding.UTF8.GetString(buffer, 0, count));
            if (input.ToString().EndsWith('z'))
                return input.ToString()[..^1];
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 20)]
    private struct ConsoleInputRecord
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public short X;
        [FieldOffset(6)] public short Y;
        [FieldOffset(8)] public uint Buttons;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(4)] public int KeyDown;
        [FieldOffset(8)] public ushort RepeatCount;
        [FieldOffset(14)] public char Character;
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(nint handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(nint handle, uint mode);

    [DllImport("kernel32.dll", EntryPoint = "WriteConsoleInputW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleInput(
        nint input, [In] ConsoleInputRecord[] records, uint count, out uint written);

    [TestMethod]
    public async Task PtyIoChild()
    {
        if (Environment.GetEnvironmentVariable(PtyIoChildEnvironmentVariable) != "1")
            return;

        using var driver = new WindowsConsoleDriver();
        driver.EnterRawMode();
        driver.Resized += (width, height) =>
            driver.Write(Encoding.UTF8.GetBytes($"PTY_RESIZED:{width}x{height};\r\n"));
        driver.Write(Encoding.UTF8.GetBytes($"PTY_READY:{driver.Width}x{driver.Height};\r\n"));

        var buffer = new byte[32];
        while (true)
        {
            var count = await driver.ReadAsync(buffer, TestContext.Current.CancellationToken);
            Assert.IsTrue(count > 0, "PTY probe input ended before the quit command.");
            for (var i = 0; i < count; i++)
            {
                var input = (char)buffer[i];
                if (input == 'q')
                    return;

                Assert.IsTrue(input is 'a' or 'b' or 'c', $"Unexpected PTY probe input: {(int)input}");
                driver.Write(Encoding.UTF8.GetBytes($"PTY_INPUT:{input}:{driver.Width}x{driver.Height};\r\n"));
            }
        }
    }

    [TestMethod]
    [TestCategory("Windows")]
    [DataRow("OK", true)]
    [DataRow("ENOTSUP", false)]
    [DataRow(null, false)]
    public async Task EnterRawModeAsync_ThroughConPty_DiscoversKgpAndPreservesInput(string? reply, bool supported)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromSeconds(30));
        var ct = cancellation.Token;
        await using var pty = new WindowsProxyPtyHandle();
        await pty.StartAsync(
            Path.Combine(AppContext.BaseDirectory, "Hex1b.Tests.exe"),
            ["--filter", "FullyQualifiedName=Hex1b.Tests.WindowsConsoleProbeTests.ProbeChild", "--no-progress", "--no-ansi"],
            AppContext.BaseDirectory,
            new Dictionary<string, string> { [ChildEnvironmentVariable] = "1" },
            80, 24, ct);

        var output = new StringBuilder();
        var replied = false;
        var expected = $"PROBE_RESULT:{supported}:abc";
        while (!output.ToString().Contains(expected, StringComparison.Ordinal))
        {
            var bytes = await pty.ReadAsync(ct);
            Assert.IsFalse(bytes.IsEmpty, $"Child exited before reporting capabilities: {output}");
            output.Append(Encoding.UTF8.GetString(bytes.Span));
            if (!replied && output.ToString().Contains(ProbeStarted, StringComparison.Ordinal))
            {
                replied = true;
                // Exercise replies larger than a ReadConsoleInput batch, mixed with keyboard input.
                if (reply is not null)
                {
                    foreach (var value in Encoding.UTF8.GetBytes($"\x1b_Gi=2147483647;{reply}\x1b\\"))
                        await pty.WriteAsync(new byte[] { value }, ct);
                }
                await pty.WriteAsync("abc"u8.ToArray(), ct);
            }
        }

        Assert.IsTrue(replied, $"Child did not start probing: {output}");
        await pty.WriteAsync("z\r"u8.ToArray(), ct);
        Assert.AreEqual(0, await pty.WaitForExitAsync(ct), output.ToString());
    }

    [TestMethod]
    public async Task ProbeChild()
    {
        if (Environment.GetEnvironmentVariable(ChildEnvironmentVariable) != "1")
            return;

        using var driver = new WindowsConsoleDriver();
        await using var adapter = new ConsolePresentationAdapter(
            driver, kgpProbeTimeout: TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var probe = adapter.EnterRawModeAsync(cancellation.Token);
        // Some ConPTY hosts consume APC queries instead of forwarding them.
        // Signal after the probe starts so input coverage does not require graphics passthrough.
        driver.Write(Encoding.UTF8.GetBytes(ProbeStarted + "\r\n"));
        await probe;

        var input = new StringBuilder();
        while (input.Length < 3)
        {
            var bytes = await adapter.ReadInputAsync(cancellation.Token);
            Assert.IsFalse(bytes.IsEmpty);
            input.Append(Encoding.UTF8.GetString(bytes.Span));
        }

        driver.Write(Encoding.UTF8.GetBytes($"PROBE_RESULT:{adapter.Capabilities.SupportsKgp}:{input}\r\n"));

        input.Clear();
        while (input.Length < 2)
        {
            var bytes = await adapter.ReadInputAsync(cancellation.Token);
            Assert.IsFalse(bytes.IsEmpty);
            input.Append(Encoding.UTF8.GetString(bytes.Span));
        }
        Assert.AreEqual("z\r", input.ToString());
    }
}
